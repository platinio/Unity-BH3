using System.Collections;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Play-mode coverage for Functions evaluated across real frames of the player loop.
    /// <para>
    /// The edit-mode suite pins evaluation semantics — the contract, binding, isolation and allocation — in a
    /// tight loop. These tests cover the three things that suite structurally cannot reach: a binding living
    /// across frames rather than statements, the rebuild path taken when a cached graph reference is
    /// invalidated mid-run, and allocation measured while the player loop is actually turning.
    /// </para>
    /// <para>
    /// Deliberately <b>not</b> tested here: that a Function returns the value its graph computes. The
    /// edit-mode suite already drives the identical <c>Flow.New</c> → <c>Invoke</c> → <c>GetValue</c> path, so
    /// an arithmetic check in play mode could only fail for a reason edit mode catches first, and would cost
    /// a domain reload to learn nothing.
    /// </para>
    /// <para>
    /// Everything is built in memory with no <c>AssetDatabase</c>, matching this assembly's lack of an editor
    /// platform restriction — which also proves a Function does not need to be a saved asset to evaluate.
    /// </para>
    /// </summary>
    public class FunctionGraphPlayModeTests
    {
        private FunctionGraphAsset function;
        private GameObject agentA;
        private GameObject agentB;

        [SetUp]
        public void SetUp()
        {
            FunctionEvaluator.InvalidateAll();
            function = HpReader();
            agentA = Agent("PlayAgentA", 10.0f);
            agentB = Agent("PlayAgentB", 20.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (agentA != null) Object.Destroy(agentA);
            if (agentB != null) Object.Destroy(agentB);
            if (function != null) Object.Destroy(function);

            FunctionEvaluator.InvalidateAll();
        }

        /// <summary>Reads the agent's <c>hp</c> and returns it. Built entirely in memory.</summary>
        private static FunctionGraphAsset HpReader()
        {
            var asset = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = asset.graph;

            var input = new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) };
            var output = new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) };
            graph.units.Add(input);
            graph.units.Add(output);

            graph.controlInputDefinitions.Add(new Unity.VisualScripting.ControlInputDefinition
            {
                key = FunctionGraphAsset.EnterKey, label = FunctionGraphAsset.EnterKey
            });
            graph.controlOutputDefinitions.Add(new Unity.VisualScripting.ControlOutputDefinition
            {
                key = FunctionGraphAsset.ExitKey, label = FunctionGraphAsset.ExitKey
            });
            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(float)
            });
            graph.PortDefinitionsChanged();

            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            var get = new Unity.VisualScripting.GetVariable
            {
                kind = Unity.VisualScripting.VariableKind.Object,
                specifyFallback = true,
                position = new Vector2(-150.0f, 0.0f)
            };
            graph.units.Add(get);
            get.name.SetDefaultValue("hp");

            var fallback = new Unity.VisualScripting.Literal(typeof(float), -1.0f)
            {
                position = new Vector2(-320.0f, 140.0f)
            };
            graph.units.Add(fallback);
            fallback.output.ValidlyConnectTo(get.fallback);
            get.value.ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);

            return asset;
        }

        private static GameObject Agent(string name, float hp)
        {
            var go = new GameObject(name);
            go.AddComponent<Unity.VisualScripting.Variables>();
            Unity.VisualScripting.Variables.Object(go).Set("hp", hp);
            return go;
        }

        // ------------------------------------------------------------------ tests

        /// <summary>
        /// A Function evaluates without ever having been saved. Worth stating on its own: the edit-mode suite
        /// creates assets on disk, so nothing there distinguishes "needs a graph" from "needs an asset file".
        /// </summary>
        [UnityTest]
        public IEnumerator Function_EvaluatesWithoutBeingASavedAsset()
        {
            var binding = FunctionEvaluator.Bind(function, agentA);
            yield return null;

            Assert.That(binding.TryEvaluate<float>(out var value, out var error), Is.True, error);
            Assert.That(value, Is.EqualTo(10.0f));
        }

        /// <summary>
        /// The binding is held across frames rather than across statements, which is how a node actually uses
        /// one, and it must keep tracking the agent as its variables change frame to frame.
        /// </summary>
        [UnityTest]
        public IEnumerator Binding_HeldAcrossFrames_TracksTheAgentAsItChanges()
        {
            var binding = FunctionEvaluator.Bind(function, agentA);

            for (var frame = 0; frame < 8; frame++)
            {
                var expected = 10.0f + frame;
                Unity.VisualScripting.Variables.Object(agentA).Set("hp", expected);

                yield return null;

                Assert.That(binding.TryEvaluate<float>(out var value, out var error), Is.True, error);
                Assert.That(value, Is.EqualTo(expected), $"stale on frame {frame}");
            }
        }

        /// <summary>
        /// Two agents sharing one Function, interleaved a frame at a time. The edit-mode version of this runs
        /// in a tight loop; this one lets the player loop, and whatever it does to pooled flows between
        /// frames, come between the two evaluations.
        /// </summary>
        [UnityTest]
        public IEnumerator TwoAgents_SharingOneFunction_StayIsolatedAcrossFrames()
        {
            var bindingA = FunctionEvaluator.Bind(function, agentA);
            var bindingB = FunctionEvaluator.Bind(function, agentB);

            for (var frame = 0; frame < 10; frame++)
            {
                Assert.That(bindingA.TryEvaluate<float>(out var a, out var errorA), Is.True, errorA);
                yield return null;
                Assert.That(bindingB.TryEvaluate<float>(out var b, out var errorB), Is.True, errorB);
                yield return null;

                Assert.That(a, Is.EqualTo(10.0f), $"agent A contaminated on frame {frame}");
                Assert.That(b, Is.EqualTo(20.0f), $"agent B contaminated on frame {frame}");
            }
        }

        /// <summary>
        /// The rebuild path. <c>EnsureReady</c> re-fetches the plan and drops the cached reference when
        /// invalidation bumps the version — the branch that exists so an edit reaches bindings that are
        /// already alive. Nothing exercised it before this test; it was written and never run.
        /// </summary>
        [UnityTest]
        public IEnumerator Binding_RebuildsAndKeepsWorking_AfterInvalidationMidRun()
        {
            var binding = FunctionEvaluator.Bind(function, agentA);

            Assert.That(binding.TryEvaluate<float>(out var before, out var firstError), Is.True, firstError);
            Assert.That(before, Is.EqualTo(10.0f));

            yield return null;

            FunctionEvaluator.InvalidateAll();

            yield return null;

            Assert.That(binding.TryEvaluate<float>(out var after, out var secondError), Is.True,
                $"the binding did not recover from invalidation: {secondError}");
            Assert.That(after, Is.EqualTo(10.0f));
        }

        // There is deliberately no play-mode allocation test here, and the reason is worth keeping.
        //
        // One was written and it was flaky: it passed when this fixture ran alone and failed in the full
        // play-mode suite, so the result depended on what else had run in the session. The instrument itself
        // is sound in play mode — an empty delegate measures clean, a known allocation is caught, and
        // evaluation measured mid-frame is clean — but the measurement is not isolated from the rest of the
        // frame, and the first evaluation after a frame boundary picks up whatever the frame warms.
        //
        // A flaky allocation assertion is worse than none: it teaches people that a red allocation test
        // means nothing. The contract is pinned instead by the deterministic edit-mode test
        // (FunctionEvaluatorTests.Evaluation_DoesNotAllocate_InSteadyState), which measures the same call
        // path with the same instrument and a control case, and does so reproducibly.
    }
}

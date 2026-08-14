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

        /// <summary>
        /// The early-return case: a loop that returns the item it is looking at rather than running to
        /// completion.
        /// <para>
        /// This is the one shape whose soundness was argued rather than measured. <c>Result</c> is
        /// <em>pulled</em> from the taken exit after the flow has finished, so returning a loop's current
        /// item only works if that value is still the one captured when the exit ran. If the port is instead
        /// re-read after the loop has moved on, the Function silently returns the wrong element — and under
        /// Tier 2, generated C# would return the right one, making it a build-only divergence.
        /// </para>
        /// <para>
        /// The loop walks 10, 20, 30 and exits on the first item. A result of 10 means the captured value
        /// survives. A result of 30 means it was pulled after the loop had advanced, which would make early
        /// return unsound for transient values.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator EarlyReturnFromALoop_ReturnsTheItemItExitedOn()
        {
            var asset = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = asset.graph;

            var input = new ScriptGraphInput { position = new Vector2(-500.0f, 0.0f) };
            var earlyExit = new ScriptGraphOutput { position = new Vector2(400.0f, -120.0f) };
            var normalExit = new ScriptGraphOutput { position = new Vector2(400.0f, 160.0f) };
            graph.units.Add(input);
            graph.units.Add(earlyExit);
            graph.units.Add(normalExit);

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
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(object)
            });
            graph.PortDefinitionsChanged();

            var items = new Unity.VisualScripting.Literal(
                typeof(System.Collections.IEnumerable),
                new System.Collections.Generic.List<object> { 10.0f, 20.0f, 30.0f })
            {
                position = new Vector2(-320.0f, 140.0f)
            };
            graph.units.Add(items);

            var loop = new Unity.VisualScripting.ForEach { position = new Vector2(-140.0f, 0.0f) };
            graph.units.Add(loop);
            items.output.ValidlyConnectTo(loop.collection);

            var missed = new Unity.VisualScripting.Literal(typeof(object), -1.0f)
            {
                position = new Vector2(200.0f, 260.0f)
            };
            graph.units.Add(missed);

            input.controlOutputs[FunctionGraphAsset.EnterKey].ValidlyConnectTo(loop.enter);

            // Exit on the very first item.
            loop.body.ValidlyConnectTo(earlyExit.controlInputs[FunctionGraphAsset.ExitKey]);
            loop.currentItem.ValidlyConnectTo(earlyExit.valueInputs[FunctionGraphAsset.ResultKey]);

            // Reaching here means the loop ran to completion instead.
            loop.exit.ValidlyConnectTo(normalExit.controlInputs[FunctionGraphAsset.ExitKey]);
            missed.output.ValidlyConnectTo(normalExit.valueInputs[FunctionGraphAsset.ResultKey]);

            var plan = FunctionBindingPlan.Resolve(asset);
            Assert.That(plan.IsUsable, Is.True, plan.Error);
            Assert.That(plan.Exits.Length, Is.EqualTo(2));

            var binding = FunctionEvaluator.Bind(asset, agentA);
            yield return null;

            Assert.That(binding.TryEvaluate<object>(out var result, out var error), Is.True, error);

            var value = System.Convert.ToSingle(result);

            Assert.That(value, Is.Not.EqualTo(-1.0f),
                "the loop ran to completion — control never reached the early exit");
            Assert.That(value, Is.EqualTo(10.0f),
                "early return did not capture the item it exited on. 30 means Result was pulled after the " +
                "loop had advanced, which makes early return unsound for transient values and would diverge " +
                "from generated C# under Tier 2");

            Object.Destroy(asset);
        }

        /// <summary>
        /// The pattern actually authored in this project: break out of the loop, fall through to the exit,
        /// and read the item you stopped on.
        /// <para>
        /// This shape was already correct before the capture-at-exit fix, and it is worth a test saying so.
        /// Because <c>Break</c> genuinely stops the loop, <c>currentItem</c> is not advanced afterwards, so
        /// pulling <c>Result</c> once the flow finishes reads the item that was current when the break
        /// happened. It needs only one exit — the loop's own exit — which is why it never ran into the
        /// multi-exit machinery at all.
        /// </para>
        /// <para>
        /// The contrast with <see cref="EarlyReturnFromALoop_ReturnsTheItemItExitedOn"/> is the point: routing
        /// to an exit without breaking leaves the loop running, and that is the case that used to return the
        /// wrong element.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator BreakingOutOfALoop_ThenExiting_ReadsTheItemItStoppedOn()
        {
            var asset = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = asset.graph;

            var input = new ScriptGraphInput { position = new Vector2(-500.0f, 0.0f) };
            var exit = new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) };
            graph.units.Add(input);
            graph.units.Add(exit);

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
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(object)
            });
            graph.PortDefinitionsChanged();

            var items = new Unity.VisualScripting.Literal(
                typeof(System.Collections.IEnumerable),
                new System.Collections.Generic.List<object> { 10.0f, 20.0f, 30.0f })
            {
                position = new Vector2(-320.0f, 140.0f)
            };
            graph.units.Add(items);

            var loop = new Unity.VisualScripting.ForEach { position = new Vector2(-140.0f, 0.0f) };
            graph.units.Add(loop);
            items.output.ValidlyConnectTo(loop.collection);

            var stop = new Unity.VisualScripting.Break { position = new Vector2(120.0f, -120.0f) };
            graph.units.Add(stop);

            input.controlOutputs[FunctionGraphAsset.EnterKey].ValidlyConnectTo(loop.enter);

            // Break on the first item, then let the loop's own exit carry control out.
            loop.body.ValidlyConnectTo(stop.enter);
            loop.exit.ValidlyConnectTo(exit.controlInputs[FunctionGraphAsset.ExitKey]);
            loop.currentItem.ValidlyConnectTo(exit.valueInputs[FunctionGraphAsset.ResultKey]);

            var binding = FunctionEvaluator.Bind(asset, agentA);
            yield return null;

            Assert.That(binding.TryEvaluate<object>(out var result, out var error), Is.True, error);
            Assert.That(System.Convert.ToSingle(result), Is.EqualTo(10.0f),
                "breaking on the first item should leave currentItem holding it");

            Object.Destroy(asset);
        }

        /// <summary>
        /// Builds a Function with one exit whose <c>Result</c> is wired to <paramref name="wire"/>, evaluated
        /// against a loop the caller sets up. Single exit on purpose: nothing is captured, so the result is
        /// read by pulling the port after the flow ends — which makes the value evidence of whether the loop
        /// actually stopped, rather than evidence of the capture fix.
        /// </summary>
        private static FunctionGraphAsset LoopFunction(
            System.Func<ScriptGraphInput, ScriptGraphOutput, Unity.VisualScripting.FlowGraph, Unity.VisualScripting.ValueOutput> wire)
        {
            var asset = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = asset.graph;

            var input = new ScriptGraphInput { position = new Vector2(-500.0f, 0.0f) };
            var exit = new ScriptGraphOutput { position = new Vector2(500.0f, 0.0f) };
            graph.units.Add(input);
            graph.units.Add(exit);

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
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(object)
            });
            graph.PortDefinitionsChanged();

            var source = wire(input, exit, graph);
            source.ValidlyConnectTo(exit.valueInputs[FunctionGraphAsset.ResultKey]);

            return asset;
        }

        private static Unity.VisualScripting.Literal Items(Unity.VisualScripting.FlowGraph graph, params object[] values)
        {
            var literal = new Unity.VisualScripting.Literal(
                typeof(System.Collections.IEnumerable),
                new System.Collections.Generic.List<object>(values));
            graph.units.Add(literal);
            return literal;
        }

        /// <summary>
        /// One exit, reached from inside a loop, with no break node. The loop must stop.
        /// <para>
        /// Nothing is captured here — a single-exit Function pulls its result after the flow ends. So a
        /// result of 10 can only mean the loop genuinely stopped on the first item, and 30 would mean it ran
        /// to completion with the exit merely ending its own branch.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ReachingAnExitInsideALoop_StopsTheLoop()
        {
            var asset = LoopFunction((input, exit, graph) =>
            {
                var loop = new Unity.VisualScripting.ForEach { position = new Vector2(-140.0f, 0.0f) };
                graph.units.Add(loop);
                Items(graph, 10.0f, 20.0f, 30.0f).output.ValidlyConnectTo(loop.collection);

                input.controlOutputs[FunctionGraphAsset.EnterKey].ValidlyConnectTo(loop.enter);
                loop.body.ValidlyConnectTo(exit.controlInputs[FunctionGraphAsset.ExitKey]);

                return loop.currentItem;
            });

            var binding = FunctionEvaluator.Bind(asset, agentA);
            yield return null;

            Assert.That(binding.TryEvaluate<object>(out var result, out var error), Is.True, error);
            Assert.That(System.Convert.ToSingle(result), Is.EqualTo(10.0f),
                "the loop kept iterating after the exit was reached — 30 means it ran to completion");

            Object.Destroy(asset);
        }

        /// <summary>
        /// The claim I previously got wrong: that an exit could only unwind one loop level.
        /// <para>
        /// Two nested loops, exiting from the inner one. The result is the <b>outer</b> loop's index, which
        /// discriminates precisely: 0 means both loops stopped, 1 means only the inner one broke and the
        /// outer carried on to its second iteration. Popping the loop stack until it is empty is what makes
        /// this 0 at any nesting depth.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ReachingAnExitInsideNestedLoops_UnwindsAllOfThem()
        {
            var asset = LoopFunction((input, exit, graph) =>
            {
                var outer = new Unity.VisualScripting.ForEach { position = new Vector2(-300.0f, 0.0f) };
                var inner = new Unity.VisualScripting.ForEach { position = new Vector2(-60.0f, 0.0f) };
                graph.units.Add(outer);
                graph.units.Add(inner);

                Items(graph, 1.0f, 2.0f).output.ValidlyConnectTo(outer.collection);
                Items(graph, 10.0f, 20.0f, 30.0f).output.ValidlyConnectTo(inner.collection);

                input.controlOutputs[FunctionGraphAsset.EnterKey].ValidlyConnectTo(outer.enter);
                outer.body.ValidlyConnectTo(inner.enter);
                inner.body.ValidlyConnectTo(exit.controlInputs[FunctionGraphAsset.ExitKey]);

                return outer.currentIndex;
            });

            var binding = FunctionEvaluator.Bind(asset, agentA);
            yield return null;

            Assert.That(binding.TryEvaluate<object>(out var result, out var error), Is.True, error);
            Assert.That(System.Convert.ToInt32(result), Is.EqualTo(0),
                "the outer loop kept running — 1 means only the inner loop was unwound");

            Object.Destroy(asset);
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

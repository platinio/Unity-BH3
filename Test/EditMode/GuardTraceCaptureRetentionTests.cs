using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Capturing a guard trace must not retain anything the trace ring does not hold (Unity-BH3#32).
    ///
    /// <para>
    /// A trace is the expensive kind of record: it re-pulls every port feeding the guard, which runs any
    /// Function in the chain, and it harvests that Function's wire values out of Visual Scripting's debug
    /// data. Each of those steps allocates, and all of it is meant to be garbage the moment the trace is
    /// built -- the ring keeps the last 64 traces and nothing else. The editor once grew by the better part
    /// of a gigabyte a minute on exactly this path, so the bound below is what stands between a working
    /// debugger and one that exhausts memory in a long playtest.
    /// </para>
    ///
    /// <para>
    /// Driven through the recorder rather than <see cref="GuardTraceCapture"/> directly, because the
    /// recorder is where the transition filter, the ring and the capture meet; a leak in any of the three
    /// shows up here. The guard is fed by a Script Graph Variable reading a Function so the chain has a
    /// real Function in it -- a trace over plain ports would leave the Visual Scripting half untested, and
    /// that half is where the retention lived.
    /// </para>
    /// </summary>
    public class GuardTraceCaptureRetentionTests
    {
        /// <summary>
        /// Calibrated, not guessed. Measured after a full collection, the clean path retains nothing at all
        /// over two thousand transitions, and holding on to just the trace objects themselves -- the smallest
        /// leak the capture path could have -- retains about 1.1 MB. This sits well clear of both, so a leak
        /// of even one trace-sized object per transition trips it and collector noise does not.
        /// </summary>
        private const long MaxRetainedBytes = 256L * 1024L;

        private const int Transitions = 2000;

        private GameObject agent;
        private FunctionGraphAsset function;
        private BehaviorTreeFlightRecorder recorder;
        private BehaviorTreeNode owner;
        private BooleanReactiveGuard guard;

        private bool recordingWasEnabled;
        private bool tracingWasEnabled;

        [SetUp]
        public void SetUp()
        {
            recordingWasEnabled = BehaviorTreeFlightRecorders.GloballyEnabled;
            tracingWasEnabled = BehaviorTreeFlightRecorders.TracingGloballyEnabled;
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
            BehaviorTreeFlightRecorders.TracingGloballyEnabled = true;

            FunctionEvaluator.InvalidateAll();

            agent = new GameObject("Agent");
            var machine = agent.AddComponent<BindableMachine>();
            machine.BindVariables();
            machine.Variables.declarations.Set("hasTarget", true);

            function = HasTargetFunction();

            var graph = new BehaviorTreeGraph();

            var sequence = new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(sequence);

            guard = new BooleanReactiveGuard { Position = new Rect(-150.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(guard);
            guard.UpdateOwner(sequence);

            var read = new VisualScriptGraphVariable { Position = new Rect(-300.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(read);
            read.SetFunction(function);
            read.Output.ValidlyConnectTo(guard.Value);

            foreach (var node in graph.Nodes) node.SetMachine(machine);

            owner = sequence;
            recorder = new BehaviorTreeFlightRecorder("Agent", "Tree");
        }

        [TearDown]
        public void TearDown()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = recordingWasEnabled;
            BehaviorTreeFlightRecorders.TracingGloballyEnabled = tracingWasEnabled;

            FunctionEvaluator.InvalidateAll();

            if (agent != null) Object.DestroyImmediate(agent);
            if (function != null) Object.DestroyImmediate(function);
        }

        /// <summary>
        /// The precondition for the bound below: a transition here really does run the Function and harvest
        /// its wires. Without this, a flat heap could mean the capture was skipped rather than that it
        /// cleaned up after itself.
        /// </summary>
        [Test]
        public void ATransitionTracesTheFunctionAndItsWires()
        {
            recorder.GuardEval(owner, guard, true);

            Assert.AreEqual(1, recorder.TraceCount, "a first result is a transition and is traced");

            var trace = recorder.TraceAt(0);

            Assert.AreEqual(2, trace.Chain.Count, trace.Describe());
            Assert.AreEqual("True", trace.Chain[1].Value,
                "the Function was re-pulled and read the agent's hasTarget");
            Assert.IsTrue(trace.Chain[1].HasSnapshot, "the Function node carries its graph snapshot");
            Assert.AreEqual(1, trace.Snapshots.Count);
            Assert.Greater(trace.Snapshots[0].Wires.Count, 0,
                "the Function's interior wires were harvested");
        }

        [Test]
        public void ThousandsOfTransitionsRetainNothingBeyondTheRing()
        {
            // Warm up: the first evaluations build Visual Scripting's reflection caches and the Function's
            // binding plan, which are one-time costs and not what is being measured.
            for (int i = 0; i < GuardTraceRing.DefaultCapacity; i++) recorder.GuardEval(owner, guard, i % 2 == 0);

            long before = Settled();

            for (int i = 0; i < Transitions; i++) recorder.GuardEval(owner, guard, i % 2 == 0);

            long after = Settled();
            long retained = after - before;

            TestContext.Out.WriteLine(
                $"[GuardTraceRetention] {Transitions} transitions retained {retained / 1024.0:F1} KB; " +
                $"ring holds {recorder.TraceCount}, dropped {recorder.Traces.Dropped}");

            Assert.AreEqual(GuardTraceRing.DefaultCapacity, recorder.TraceCount, "the ring is full and no larger");
            Assert.AreEqual(Transitions, recorder.Traces.Dropped, "every transition past the warm-up scrolled one off");

            Assert.LessOrEqual(retained, MaxRetainedBytes,
                $"{retained / 1024.0:F1} KB retained over {Transitions} transitions. " +
                "Capturing traces retained managed memory beyond the ring. The ring is bounded, so this is " +
                "the capture path itself holding on to something per trace -- a re-pulled Function's flow, " +
                "its graph reference, or the wire snapshot -- and it compounds for the whole session.");
        }

        /// <summary>Managed bytes in use after a full collection.</summary>
        private static long Settled()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            return System.GC.GetTotalMemory(true);
        }

        /// <summary>
        /// A predicate Function returning the agent's <c>hasTarget</c>, the shape a guard in a real tree
        /// reads. In memory: the binding plan reads the graph, which needs no asset on disk.
        /// </summary>
        private static FunctionGraphAsset HasTargetFunction()
        {
            var asset = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = asset.graph;

            var input = new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) };
            var output = new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) };
            graph.units.Add(input);
            graph.units.Add(output);

            // Fully qualified: inside ArcaneOnyx.BehaviorTree.* these names resolve to BH3's own port
            // definition types, which are unrelated to Visual Scripting's despite the identical spelling.
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
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(bool)
            });
            graph.PortDefinitionsChanged();

            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            var read = new Unity.VisualScripting.GetVariable
            {
                kind = VariableKind.Object,
                specifyFallback = true,
                position = new Vector2(-160.0f, 0.0f)
            };
            graph.units.Add(read);
            read.name.SetDefaultValue("hasTarget");

            var fallback = new Unity.VisualScripting.Literal(typeof(bool), false)
            {
                position = new Vector2(-320.0f, 120.0f)
            };
            graph.units.Add(fallback);
            fallback.output.ValidlyConnectTo(read.fallback);

            read.value.ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);

            asset.SetWatchedKeys(new[] { "hasTarget" });

            return asset;
        }

        /// <summary>
        /// A machine whose <see cref="BehaviorTreeMachine.Variables"/> can be bound outside play mode, where
        /// <c>Awake</c> does not run. The Function reads the agent through it.
        /// </summary>
        private sealed class BindableMachine : BehaviorTreeMachine
        {
            public void BindVariables() => Variables = GetComponent<Variables>();
        }
    }
}

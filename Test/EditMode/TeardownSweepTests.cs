using System.Linq;
using System.Text.RegularExpressions;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A teardown has to finish whatever a node's <c>OnExit</c> does (Unity-BH3#35).
    ///
    /// <para>
    /// <c>OnExit</c> is author code and <c>OnNodeExit</c> lets whatever it throws out. Every place that exits
    /// nodes as one step of something larger — a container sweeping its children, a composite exiting a
    /// child and then advancing its resume index, a sub-tree call exiting the instance it drives — used to
    /// stop at the first throw. What that leaves behind is quieter than the exception: siblings marked
    /// running with nothing left to exit them, entered a second time on the next entry with no exit in
    /// between, or a composite resuming next frame on a child whose <c>OnExit</c> already ran.
    /// </para>
    ///
    /// <para>
    /// The entry unwind closed this shape first, in <c>EntryVerdictTests.AnUnwindKeepsGoingWhenAnExitThrowsToo</c>.
    /// These are the same assertion on the ordinary teardown paths. Each expects the fault to be logged —
    /// swallowing it silently would be the wrong trade — and each checks that the node which raised it still
    /// reads <see cref="ExecutionStatus.Exception"/>, so nothing about the failure is hidden by the fix.
    /// </para>
    /// </summary>
    [TestFixture]
    public class TeardownSweepTests
    {
        private BehaviorTreeGraphAsset[] created = System.Array.Empty<BehaviorTreeGraphAsset>();

        [TearDown]
        public void DestroyAssets()
        {
            foreach (var asset in created)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }

            created = System.Array.Empty<BehaviorTreeGraphAsset>();
        }

        private static readonly Regex ExitFault = new("InvalidOperationException: OnExit failed");

        private static T Add<T>(BehaviorTreeGraph graph, float x, float y = 300.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, y, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, graph.CountTransitionsFromNode(parent));
            graph.Transitions.Add(transition);
        }

        // ------------------------------------------------------------------ the container sweep

        /// <summary>
        /// The issue as reported. A Parallel with a child that faults on exit and a sibling after it: the
        /// sweep must reach the sibling.
        /// </summary>
        [Test]
        public void AContainerSweepKeepsGoingWhenAChildThrowsOnExit()
        {
            var graph = new BehaviorTreeGraph();
            var parallel = Add<ParallelSequence>(graph, 0.0f, 100.0f);
            var faults = Add<ThrowsOnExitNode>(graph, -200.0f);
            var wouldBeStranded = Add<ScriptedNode>(graph, 200.0f);
            wouldBeStranded.DefaultResult = ExecutionStatus.Running;

            Connect(graph, graph.EntryNode, parallel);
            Connect(graph, parallel, faults);
            Connect(graph, parallel, wouldBeStranded);

            graph.OnAwake();
            parallel.OnNodeEnter();
            parallel.OnUpdateInternal();

            Assert.IsTrue(wouldBeStranded.IsRunning, "the fixture must have both children mid-run before the sweep");

            LogAssert.Expect(LogType.Exception, ExitFault);

            Assert.DoesNotThrow(() => parallel.OnNodeExit(),
                "a fault in one child's OnExit is that child's problem, not the container's");

            Assert.AreEqual(1, wouldBeStranded.ExitCalls,
                "the sibling after the faulting child is exactly the one the sweep exists to reach");
            Assert.IsFalse(wouldBeStranded.IsRunning);
            Assert.IsFalse(parallel.IsRunning);
            Assert.AreEqual(ExecutionStatus.Exception, faults.LastExecutionStatus,
                "and the fault stays visible on the node that raised it");
        }

        /// <summary>
        /// <c>Entry</c> carries its own copy of the sweep and is what <c>BehaviorTreeGraph.OnExit</c> reaches
        /// first, so this is also the machine's <c>ReleaseTree</c>: a throw escaping here would skip the
        /// graph's <c>OnDestroy</c> and leak the instance.
        /// </summary>
        [Test]
        public void AGraphExitSurvivesTheRootChildThrowingOnExit()
        {
            var graph = new BehaviorTreeGraph();
            var faults = Add<ThrowsOnExitNode>(graph, 0.0f, 100.0f);

            Connect(graph, graph.EntryNode, faults);

            graph.OnAwake();
            graph.OnEnter();
            graph.OnUpdate();

            Assert.IsTrue(faults.IsRunning, "the fixture must have the branch mid-run before the graph exits");

            LogAssert.Expect(LogType.Exception, ExitFault);

            Assert.DoesNotThrow(() => graph.OnExit());

            Assert.IsFalse(faults.IsRunning);
            Assert.IsFalse(graph.EntryNode.IsRunning);
        }

        // ------------------------------------------------------------------ composites exiting one child

        /// <summary>
        /// The preempted victim. A takeover exits the loser and then moves the resume index to the winner;
        /// if the exit threw between those two steps, the selector would resume next frame on the victim it
        /// had already exited, and tick it without ever re-entering it.
        /// </summary>
        [Test]
        public void ASelectorTakeoverCompletesWhenTheVictimThrowsOnExit()
        {
            var graph = new BehaviorTreeGraph();
            var selector = Add<Selector>(graph, 0.0f, 100.0f);
            var high = Add<ScriptedNode>(graph, -200.0f);
            high.DefaultResult = ExecutionStatus.Running;
            var low = Add<ThrowsOnExitNode>(graph, 200.0f);

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, high);
            Connect(graph, selector, low);

            var bid = Add<CountingReactiveGuard>(graph, -400.0f);
            bid.UpdateOwner(high);
            bid.Result = false;

            graph.OnAwake();
            selector.OnNodeEnter();
            selector.OnUpdateInternal();

            Assert.IsTrue(low.IsRunning, "the fixture starts with the lower-priority branch holding the slot");

            bid.Result = true;

            LogAssert.Expect(LogType.Exception, ExitFault);

            ExecutionStatus result = default;
            Assert.DoesNotThrow(() => result = selector.OnUpdateInternal());

            Assert.AreEqual(ExecutionStatus.Running, result);
            Assert.AreEqual(1, high.EnterCalls, "the takeover must complete on the same tick the victim faulted");
            Assert.AreEqual(1, high.UpdateCalls);
            Assert.IsFalse(low.IsRunning);

            selector.OnUpdateInternal();

            Assert.AreEqual(2, high.UpdateCalls,
                "and the next tick resumes on the winner, not on the victim the selector had already exited");
            Assert.AreEqual(1, high.EnterCalls, "without entering it again");
        }

        /// <summary>
        /// The ordinary Failure path: a child fails, faults on exit, and the selector must still fall
        /// through to the next child rather than stall on the one it just exited.
        /// </summary>
        [Test]
        public void ASelectorFallsThroughWhenAFailedChildThrowsOnExit()
        {
            var graph = new BehaviorTreeGraph();
            var selector = Add<Selector>(graph, 0.0f, 100.0f);
            var fails = Add<ThrowsOnExitNode>(graph, -200.0f);
            fails.Result = ExecutionStatus.Failure;
            var next = Add<ScriptedNode>(graph, 200.0f);
            next.DefaultResult = ExecutionStatus.Running;

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, fails);
            Connect(graph, selector, next);

            graph.OnAwake();
            selector.OnNodeEnter();

            LogAssert.Expect(LogType.Exception, ExitFault);

            ExecutionStatus result = default;
            Assert.DoesNotThrow(() => result = selector.OnUpdateInternal());

            Assert.AreEqual(ExecutionStatus.Running, result);
            Assert.AreEqual(1, next.EnterCalls, "the selector moved on within the same tick");
            Assert.IsFalse(fails.IsRunning);
            Assert.AreEqual(ExecutionStatus.Exception, fails.LastExecutionStatus);

            selector.OnUpdateInternal();

            Assert.AreEqual(2, next.UpdateCalls, "and resumes on the child it moved to");
        }

        /// <summary>
        /// The Sequence twin: a child succeeds, faults on exit, and the sequence must still advance.
        /// </summary>
        [Test]
        public void ASequenceAdvancesWhenASucceededChildThrowsOnExit()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = Add<Sequence>(graph, 0.0f, 100.0f);
            var succeeds = Add<ThrowsOnExitNode>(graph, -200.0f);
            succeeds.Result = ExecutionStatus.Success;
            var next = Add<ScriptedNode>(graph, 200.0f);
            next.DefaultResult = ExecutionStatus.Running;

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, succeeds);
            Connect(graph, sequence, next);

            graph.OnAwake();
            sequence.OnNodeEnter();

            LogAssert.Expect(LogType.Exception, ExitFault);

            ExecutionStatus result = default;
            Assert.DoesNotThrow(() => result = sequence.OnUpdateInternal());

            Assert.AreEqual(ExecutionStatus.Running, result);
            Assert.AreEqual(1, next.EnterCalls, "the sequence advanced within the same tick");
            Assert.IsFalse(succeeds.IsRunning);

            sequence.OnUpdateInternal();

            Assert.AreEqual(2, next.UpdateCalls, "and resumes on the child it advanced to");
            Assert.AreEqual(1, next.EnterCalls);
        }

        // ------------------------------------------------------------------ across a sub-tree boundary

        /// <summary>
        /// Aborting a sub-tree exits every node inside the instance. A node in there faulting on exit must
        /// not stop that: the sibling beside it is inside a clone nothing else will ever reach.
        /// </summary>
        [Test]
        public void AbortingASubTreeSurvivesANodeInsideThrowingOnExit()
        {
            var branch = NewTree("Branch");
            var parallel = Add<ParallelSequence>(branch.graph, 0.0f, 100.0f);
            var faults = Add<ThrowsOnExitNode>(branch.graph, -200.0f);
            var leaf = Add<AlwaysRunningNode>(branch.graph, 200.0f);
            Connect(branch.graph, branch.graph.EntryNode, parallel);
            Connect(branch.graph, parallel, faults);
            Connect(branch.graph, parallel, leaf);

            var host = NewTree("Host");
            var selector = Add<Selector>(host.graph, 0.0f, 100.0f);
            var runNode = Add<RunBehaviorTreeGraphNode>(host.graph, 0.0f);
            runNode.SetBehaviorTreeGraphAsset(branch);
            Connect(host.graph, host.graph.EntryNode, selector);
            Connect(host.graph, selector, runNode);

            var guard = Add<CountingReactiveGuard>(host.graph, 200.0f);
            guard.UpdateOwner(runNode);

            host.graph.OnAwake();
            host.graph.OnEnter();
            host.graph.OnUpdate();

            var leafInside = runNode.BehaviorTreeGraphInstance.Nodes.OfType<AlwaysRunningNode>().Single();
            var faultsInside = runNode.BehaviorTreeGraphInstance.Nodes.OfType<ThrowsOnExitNode>().Single();

            Assert.IsTrue(leafInside.IsRunning, "the fixture must have the sub-tree mid-run before the abort");

            guard.Result = false;

            LogAssert.Expect(LogType.Exception, ExitFault);

            Assert.DoesNotThrow(() => host.graph.OnUpdate());

            Assert.AreEqual(1, leafInside.ExitCalls, "the sibling inside the instance was reached past the fault");
            Assert.IsFalse(leafInside.IsRunning);
            Assert.IsFalse(faultsInside.IsRunning);
            Assert.IsFalse(runNode.IsRunning);
        }

        private BehaviorTreeGraphAsset NewTree(string name)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            asset.name = name;

            created = created.Concat(new[] { asset }).ToArray();

            return asset;
        }
    }
}

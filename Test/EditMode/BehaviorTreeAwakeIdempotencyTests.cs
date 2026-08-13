using System.Reflection;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="BehaviorTreeGraph.OnAwake"/> builds two pieces of derived state from the graph — each
    /// container's child list, and each node's armed guards — and both were built by appending. Awakening a
    /// graph twice therefore doubled both.
    ///
    /// <para>
    /// Neither failure announces itself. Duplicated guards AND to the same boolean, and a duplicated child
    /// list still runs the right branches, just each of them twice. So the cost shows up as evaluation count
    /// and as side effects firing twice, which is precisely what these tests measure rather than trusting the
    /// visible behaviour.
    /// </para>
    ///
    /// <para>
    /// A second awake is not hypothetical: the authoring docs tell test writers to call
    /// <c>graph.OnAwake()</c> by hand after wiring guards, editor tooling warms a graph for inspection before
    /// the machine gets it, and any live-edit path re-initialises by design.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeAwakeIdempotencyTests
    {
        private static T AddNode<T>(BehaviorTreeGraph graph, float x = 0.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, 0);
            graph.Transitions.Add(transition);
        }

        private static ScriptedNode AddLeaf(BehaviorTreeGraph graph, ExecutionStatus result, float x)
        {
            var leaf = new ScriptedNode(result) { Position = new Rect(x, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(leaf);

            return leaf;
        }

        [Test]
        public void AwakingTwiceArmsEachGuardOnce()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph);
            var leaf = AddLeaf(graph, ExecutionStatus.Running, 0.0f);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, leaf);

            var guard = AddNode<CountingReactiveGuard>(graph);
            guard.UpdateOwner(sequence);

            graph.OnAwake();
            graph.OnAwake();

            Assert.AreEqual(1, sequence.ConditionalExecutions.Count,
                "Arming is rebuilt from the graph, so a second awake must not add the same guard again.");
        }

        /// <summary>
        /// The cost that duplication actually imposes. A guard is evaluated once per tick per registration,
        /// so a doubly-armed guard runs its graph twice a frame — and a guard whose graph has a side effect
        /// performs that side effect twice.
        /// </summary>
        [Test]
        public void ADoublyAwokenGuardStillEvaluatesOncePerTick()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph);
            var leaf = AddLeaf(graph, ExecutionStatus.Running, 0.0f);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, leaf);

            var guard = AddNode<CountingReactiveGuard>(graph);
            guard.UpdateOwner(sequence);

            graph.OnAwake();
            graph.OnAwake();

            sequence.OnNodeEnter();
            int afterEnter = guard.Evaluations;

            sequence.OnUpdateInternal();

            Assert.AreEqual(1, afterEnter, "Entry evaluates the guard set once.");
            Assert.AreEqual(2, guard.Evaluations,
                "and the tick evaluates it once more — two registrations would make each of these a pair.");
        }

        /// <summary>
        /// Rebuild-from-source rather than dedupe-on-insert. A guid-keyed set would also stop the count
        /// doubling, but it would keep arming a guard the designer has since deleted.
        /// </summary>
        [Test]
        public void AGuardRemovedBetweenAwakesIsNoLongerArmed()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph);
            var leaf = AddLeaf(graph, ExecutionStatus.Running, 0.0f);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, leaf);

            var guard = AddNode<CountingReactiveGuard>(graph);
            guard.UpdateOwner(sequence);

            graph.OnAwake();
            Assert.AreEqual(1, sequence.ConditionalExecutions.Count, "Armed by the first awake.");

            graph.Nodes.Remove(guard);
            graph.OnAwake();

            Assert.IsEmpty(sequence.ConditionalExecutions,
                "A guard deleted from the graph must stop gating its owner; a stale registration would keep "
                + "a branch gated on a condition that no longer exists anywhere the designer can see.");
        }

        /// <summary>
        /// The child list is the other piece of derived state <see cref="BehaviorTreeGraph.OnAwake"/> builds,
        /// and it was appended to as well. For a Selector a duplicated child list is a duplicated priority
        /// list: every branch is tried twice before the next one is reached.
        /// </summary>
        [Test]
        public void AwakingTwiceDoesNotDuplicateAContainersChildren()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var first = AddLeaf(graph, ExecutionStatus.Failure, -200.0f);
            var second = AddLeaf(graph, ExecutionStatus.Success, 200.0f);

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, first);
            Connect(graph, selector, second);

            graph.OnAwake();
            graph.OnAwake();

            Assert.AreEqual(2, selector.GetChildren().Count,
                "The child list is rebuilt from the transitions, so awaking again must not append a "
                + "second copy of every branch.");
        }

        /// <summary>
        /// The behavioural consequence of the previous test, stated as execution rather than as a count: a
        /// branch that fails must be tried once and stepped over, not tried again further down the list.
        /// </summary>
        [Test]
        public void ADoublyAwokenSelectorDoesNotRetryAFailedBranch()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var failing = AddLeaf(graph, ExecutionStatus.Failure, -200.0f);
            var succeeding = AddLeaf(graph, ExecutionStatus.Success, 200.0f);

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, failing);
            Connect(graph, selector, succeeding);

            graph.OnAwake();
            graph.OnAwake();

            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal());
            Assert.AreEqual(1, failing.UpdateCalls,
                "A failing branch is tried once. Twice means the selector is walking a duplicated list, "
                + "which also makes every side effect on that branch happen twice.");
            Assert.AreEqual(1, succeeding.UpdateCalls);
        }

        /// <summary>
        /// The guard list is <c>[DoNotSerialize]</c>, so a node that comes back from deserialization can
        /// arrive with it null — its field initialiser never ran. That went unnoticed while arming only
        /// touched nodes that own a guard; clearing every node's list walks nodes that have none, and threw.
        /// <para>
        /// The null state is reproduced directly rather than by round-tripping an asset, because the
        /// deserializer is what produces it and an edit-mode test has no asset to instantiate.
        /// </para>
        /// </summary>
        [Test]
        public void AwakingSurvivesANodeWhoseGuardListWasNeverInitialised()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);
            var leaf = AddLeaf(graph, ExecutionStatus.Success, 0.0f);

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, leaf);

            foreach (var node in new BehaviorTreeNode[] { selector, leaf, graph.EntryNode })
            {
                typeof(BehaviorTreeNode)
                    .GetField("conditionalExecutions", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(node, null);
            }

            Assert.DoesNotThrow(() => graph.OnAwake(),
                "A node with no guards must awaken whether or not its guard list exists yet.");

            Assert.IsEmpty(selector.ConditionalExecutions,
                "and reading the guards of such a node answers empty rather than throwing.");
        }

        /// <summary>
        /// Awakening once must keep working exactly as before — the fix is about repeat calls, and a
        /// regression here would be invisible in the tests above because they all awaken twice.
        /// </summary>
        [Test]
        public void AwakingOnceStillArmsGuardsAndChildren()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var first = AddLeaf(graph, ExecutionStatus.Failure, -200.0f);
            var second = AddLeaf(graph, ExecutionStatus.Success, 200.0f);

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, first);
            Connect(graph, selector, second);

            var guard = AddNode<CountingReactiveGuard>(graph);
            guard.UpdateOwner(selector);

            graph.OnAwake();

            Assert.AreEqual(1, selector.ConditionalExecutions.Count);
            Assert.AreEqual(2, selector.GetChildren().Count);
        }
    }
}

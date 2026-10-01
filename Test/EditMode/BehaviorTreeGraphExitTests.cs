using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="BehaviorTreeGraph.OnExit"/> — the mirror of <see cref="BehaviorTreeGraph.OnEnter"/>, and the
    /// only way to stop a tree without simply abandoning it.
    ///
    /// <para>
    /// It exists because <see cref="BehaviorTreeGraph.OnDestroy"/> cascades <c>OnDestroy</c> and nothing else,
    /// so a tree dropped through that alone never tells the branch that was running that it stopped: the
    /// <c>NavMeshAgent</c> keeps walking, the animation keeps playing. <c>BehaviorTreeMachine.Switch</c> drops
    /// a tree on purpose every time it is called, which is what made this the missing half of the contract.
    /// </para>
    ///
    /// <para>
    /// The whole of the mechanism is that exiting one node reaches the branch below it —
    /// <see cref="ContainerNode.OnExit"/> exits its children, and a node that never started returns
    /// immediately. So the two questions worth asking are whether the walk reaches deep enough, and whether it
    /// stops at branches that were never entered. Both are asserted through the doubles' own exit counts
    /// rather than through node status, because "was told to clean up" is the thing a leaf actually acts on.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeGraphExitTests
    {
        private static AlwaysRunningNode AddRunningLeaf(BehaviorTreeGraph graph, float x)
        {
            var leaf = new AlwaysRunningNode { Position = new Rect(x, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(leaf);

            return leaf;
        }

        /// <summary>
        /// Entry -&gt; Sequence -&gt; leaf, awoken, entered and ticked once, so the leaf is genuinely running
        /// when the test asks the graph to stop.
        /// </summary>
        private static BehaviorTreeGraph RunningTree(out AlwaysRunningNode leaf)
        {
            var graph = new BehaviorTreeGraph();
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(graph, 0.0f, 100.0f);

            leaf = AddRunningLeaf(graph, 0.0f);

            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, sequence);
            BehaviorTreeAuthoring.Connect(graph, sequence, leaf);

            graph.OnAwake();
            graph.OnEnter();
            graph.OnUpdate();

            return graph;
        }

        /// <summary>
        /// The point of the whole method: a leaf two containers below the root is told it stopped. A cascade
        /// that only reached the root's direct child would pass a shallower test and still leave every real
        /// tree's actual leaf running.
        /// </summary>
        [Test]
        public void ExitingAGraphExitsTheLeafThatWasStillRunning()
        {
            var graph = RunningTree(out var leaf);

            Assert.AreEqual(1, leaf.EnterCalls, "The leaf is running before the graph is asked to stop.");
            Assert.AreEqual(0, leaf.ExitCalls);

            graph.OnExit();

            Assert.AreEqual(1, leaf.ExitCalls,
                "A leaf two levels below the root must be told the tree stopped. Without it the branch is "
                + "abandoned mid-action -- whatever it started, it never gets the call that ends it.");
        }

        /// <summary>
        /// Status, as opposed to the callback. A node left marked running is a node the canvas and the
        /// why-panel keep reporting as live on a tree that no longer exists.
        /// </summary>
        [Test]
        public void ExitingAGraphLeavesNothingMarkedRunning()
        {
            var graph = RunningTree(out var leaf);

            graph.OnExit();

            Assert.IsFalse(leaf.IsRunning, "The leaf is no longer running.");
            Assert.IsFalse(graph.EntryNode.IsRunning, "and neither is the root it hung from.");
        }

        /// <summary>
        /// The other half of the contract. A selector's lower-priority branch was never entered, so it has
        /// nothing to clean up and must not be told otherwise — a leaf that counts exits it never earned is
        /// a leaf that will eventually stop something it never started.
        /// </summary>
        [Test]
        public void ExitingAGraphSkipsABranchThatNeverRan()
        {
            var graph = new BehaviorTreeGraph();
            var selector = BehaviorTreeAuthoring.AddNode<Selector>(graph, 0.0f, 100.0f);

            var taken = AddRunningLeaf(graph, -200.0f);
            var neverReached = AddRunningLeaf(graph, 200.0f);

            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, selector);
            BehaviorTreeAuthoring.Connect(graph, selector, taken);
            BehaviorTreeAuthoring.Connect(graph, selector, neverReached);

            graph.OnAwake();
            graph.OnEnter();
            graph.OnUpdate();

            Assert.AreEqual(0, neverReached.EnterCalls, "The first branch holds, so the second never starts.");

            graph.OnExit();

            Assert.AreEqual(1, taken.ExitCalls, "The branch that ran is stopped.");
            Assert.AreEqual(0, neverReached.ExitCalls, "and the one that never ran is left alone.");
        }

        /// <summary>
        /// Exiting is idempotent, which matters because the machine calls it on a switch and again on destroy
        /// if the agent dies in the same frame it was handed a new tree.
        /// </summary>
        [Test]
        public void ExitingAGraphTwiceExitsEachNodeOnce()
        {
            var graph = RunningTree(out var leaf);

            graph.OnExit();
            graph.OnExit();

            Assert.AreEqual(1, leaf.ExitCalls,
                "The second call has nothing left to stop. Counting it twice would run every leaf's cleanup "
                + "a second time -- releasing a claim someone else has since taken, for instance.");
        }

        /// <summary>
        /// A tree that was awoken but never entered — the machine's own state between <c>Awake</c> and
        /// <c>Start</c>, and exactly the window in which an agent can be destroyed.
        /// </summary>
        [Test]
        public void ExitingATreeThatNeverStartedIsHarmless()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(graph, 0.0f, 100.0f);
            var leaf = AddRunningLeaf(graph, 0.0f);

            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, sequence);
            BehaviorTreeAuthoring.Connect(graph, sequence, leaf);

            graph.OnAwake();

            Assert.DoesNotThrow(() => graph.OnExit(),
                "An agent destroyed between Awake and Start has a tree that was never entered.");

            Assert.AreEqual(0, leaf.ExitCalls, "and nothing in it is told to stop, because nothing started.");
        }
    }
}

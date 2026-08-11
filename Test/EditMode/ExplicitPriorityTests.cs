using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Priority used to be canvas geometry: the runtime sorted every container's children by <c>Position.x</c>
    /// at awake, so pixel coordinates were the serialized truth. Tidying a layout could silently change what
    /// an agent did, and a tree generated from code ran in whatever order its coordinates implied rather than
    /// the order its <c>Connect</c> calls asked for.
    ///
    /// <para>
    /// The transition's index is now the truth. Position is only the gesture that writes it. These pin both
    /// halves of that, and — most importantly — that a tree authored before the change still runs exactly as
    /// it did, since every such tree stored index 0 on every transition.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ExplicitPriorityTests
    {
        private static T AddNode<T>(BehaviorTreeGraph graph, float x = 0.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static ScriptedNode AddLeaf(BehaviorTreeGraph graph, ExecutionStatus result, float x)
        {
            var leaf = new ScriptedNode(result) { Position = new Rect(x, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(leaf);

            return leaf;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child, int index)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, index);
            graph.Transitions.Add(transition);
        }

        /// <summary>
        /// The headline behaviour: layout says one thing, the indices say another, and the indices win.
        /// </summary>
        [Test]
        public void ChildrenRunByIndexEvenWhenLayoutContradictsIt()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            // 'first' sits to the RIGHT of 'second' on the canvas, but claims priority 0.
            var first = AddLeaf(graph, ExecutionStatus.Success, 500.0f);
            var second = AddLeaf(graph, ExecutionStatus.Success, -500.0f);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, first, 0);
            Connect(graph, selector, second, 1);

            graph.OnAwake();

            Assert.AreSame(first, selector.GetChildren()[0],
                "The stored index is the priority; where the node sits is only how the designer set it.");
            Assert.AreSame(second, selector.GetChildren()[1]);
        }

        /// <summary>
        /// Acceptance criterion 5. Every tree authored before this change carries index 0 on every transition,
        /// because the canvas computed the index by counting edges between the same pair of nodes and a pair
        /// is connected at most once. Trusting those would collapse a container to one priority, so they are
        /// read as "no order recorded" and position answers instead.
        /// </summary>
        [Test]
        public void APreChangeTreeStillRunsInCanvasOrder()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var leftmost = AddLeaf(graph, ExecutionStatus.Success, -500.0f);
            var rightmost = AddLeaf(graph, ExecutionStatus.Success, 500.0f);

            Connect(graph, graph.EntryNode, selector, 0);

            // Both zero — what the old canvas wrote for every edge it ever created.
            Connect(graph, selector, rightmost, 0);
            Connect(graph, selector, leftmost, 0);

            graph.OnAwake();

            Assert.AreSame(leftmost, selector.GetChildren()[0],
                "With no usable indices the leftmost branch is still the one tried first, so an existing "
                + "asset behaves identically to before the change.");
            Assert.AreSame(rightmost, selector.GetChildren()[1]);
        }

        /// <summary>
        /// A gap means something was removed without renumbering and a duplicate means two branches claim one
        /// priority. Neither is an order worth trusting, so both fall back to position.
        /// </summary>
        [Test]
        public void NonContiguousIndicesFallBackToCanvasOrder()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var leftmost = AddLeaf(graph, ExecutionStatus.Success, -500.0f);
            var rightmost = AddLeaf(graph, ExecutionStatus.Success, 500.0f);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, rightmost, 0);
            Connect(graph, selector, leftmost, 7);

            graph.OnAwake();

            Assert.AreSame(leftmost, selector.GetChildren()[0],
                "0 and 7 is not a priority order over two children, so position decides.");
        }

        /// <summary>
        /// The behavioural consequence, rather than the list order: a Selector must actually try the branch
        /// its indices call first, and never reach the second when the first succeeds.
        /// </summary>
        [Test]
        public void ASelectorTriesTheIndexZeroBranchFirst()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var winner = AddLeaf(graph, ExecutionStatus.Success, 500.0f);
            var loser = AddLeaf(graph, ExecutionStatus.Success, -500.0f);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, winner, 0);
            Connect(graph, selector, loser, 1);

            graph.OnAwake();
            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal());
            Assert.AreEqual(1, winner.UpdateCalls, "The index-0 branch is the one that ran,");
            Assert.AreEqual(0, loser.UpdateCalls, "and a succeeding branch means the next is never reached.");
        }

        /// <summary>
        /// The dump reports execution order, so it has to read the same rule the runtime does. When these
        /// disagree the dump is worse than useless: it is a review tool confidently reporting the wrong
        /// priority, and it is what CI checks generated trees with.
        /// </summary>
        [Test]
        public void TheDumpReportsChildrenInIndexOrder()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            // Distinct types so each child is identifiable in the JSON by its own name.
            var first = AddNode<Sequence>(graph, 500.0f);
            var second = AddNode<Repeater>(graph, -500.0f);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, first, 0);
            Connect(graph, selector, second, 1);

            string json = BehaviorTreeDump.ToJson(graph, "Fixture");

            Assert.Less(json.IndexOf("\"type\": \"Sequence\""), json.IndexOf("\"type\": \"Repeater\""),
                "The dump must list children in the order they will be tried, not in canvas order.");
        }

        /// <summary>
        /// The why-panel numbers branches by their position in this list, so a topology that ordered children
        /// differently from the runtime would explain the wrong branch.
        /// </summary>
        [Test]
        public void TheTopologyReportsChildrenInIndexOrder()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var first = AddLeaf(graph, ExecutionStatus.Success, 500.0f);
            var second = AddLeaf(graph, ExecutionStatus.Success, -500.0f);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, first, 0);
            Connect(graph, selector, second, 1);

            var topology = BehaviorTreeGraphTopology.From(graph);

            Assert.IsTrue(topology.TryGetNode(selector.guid, out var info));
            Assert.AreEqual(first.guid, info.Children[0],
                "Priority 0 in the explanation must be priority 0 at runtime.");
            Assert.AreEqual(second.guid, info.Children[1]);
        }

        /// <summary>
        /// The random composites are the one case where a fixed order is wrong, and their only initial
        /// shuffle came from the sort call at awake that this change neutered. Removing it outright would
        /// have left them running in authored order on their first pass.
        /// </summary>
        [Test]
        public void RandomCompositesAreStillShuffledAtAwake()
        {
            const int children = 8;
            bool everDifferedFromAuthoredOrder = false;

            // A shuffle can legitimately return the authored order, so one sample proves nothing. Any single
            // ordering has a 1/8! chance, making a run of attempts that never differs a real signal.
            for (int attempt = 0; attempt < 20 && !everDifferedFromAuthoredOrder; attempt++)
            {
                var graph = new BehaviorTreeGraph();
                var random = AddNode<RandomSelector>(graph);
                var authored = new BehaviorTreeNode[children];

                Connect(graph, graph.EntryNode, random, 0);

                for (int index = 0; index < children; index++)
                {
                    authored[index] = AddLeaf(graph, ExecutionStatus.Failure, index * 100.0f);
                    Connect(graph, random, authored[index], index);
                }

                graph.OnAwake();

                for (int index = 0; index < children; index++)
                {
                    if (random.GetChildren()[index] != authored[index]) everDifferedFromAuthoredOrder = true;
                }
            }

            Assert.IsTrue(everDifferedFromAuthoredOrder,
                "A Random Selector must not run its children in authored order every time.");
        }

    }
}

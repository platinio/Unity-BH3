using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Connecting and deleting on the canvas leave a parent's child indices a clean <c>0..n-1</c>, the way a
    /// drag release already did.
    ///
    /// <para>
    /// A new connection used to take the next free index whatever its position, and a delete renumbered
    /// nothing. So a child deleted and replaced in the same spot came back as the last priority with amber
    /// badges, and stayed that way until something was dragged.
    /// </para>
    ///
    /// <para>
    /// Every fixture drives the real canvas (<c>EndTransition</c>, <c>DeleteSelection</c>,
    /// <c>SyncBookkeeping</c>) rather than a helper, because the bug was in which gestures wrote the order
    /// and which did not.
    /// </para>
    /// </summary>
    public class ChildOrderOnConnectAndDeleteTests
    {
        private const string Folder = "Assets/__ChildOrderOnConnectAndDeleteTests";

        private BehaviorTreeGraphAsset tree;
        private BehaviorTreeCanvas canvas;
        private Selector parent;
        private WaitTime left, middle, right;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__ChildOrderOnConnectAndDeleteTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");

            parent = BehaviorTreeAuthoring.AddNode<Selector>(tree, 200.0f, 0.0f);

            left = ChildAt(100.0f);
            middle = ChildAt(200.0f);
            right = ChildAt(300.0f);

            BehaviorTreeAuthoring.Connect(tree, parent, left);
            BehaviorTreeAuthoring.Connect(tree, parent, middle);
            BehaviorTreeAuthoring.Connect(tree, parent, right);

            canvas = new BehaviorTreeCanvas(tree.graph);
        }

        [TearDown]
        public void TearDown()
        {
            canvas.Close();

            AssetDatabase.DeleteAsset(Folder);
        }

        private WaitTime ChildAt(float x) => BehaviorTreeAuthoring.AddNode<WaitTime>(tree, x, 200.0f);

        private void ConnectOnCanvas(BehaviorTreeNode source, BehaviorTreeNode child)
        {
            canvas.TransitionSource = source;
            canvas.EndTransition(child);
        }

        /// <summary>Delete, then the GUI event that follows it, which is where a deleted node's wire is removed.</summary>
        private void DeleteOnCanvas(GraphCore.IGraphElement element)
        {
            canvas.selection.Clear();
            canvas.selection.Select(element);
            canvas.DeleteSelection();
            canvas.SyncBookkeeping();
        }

        private BehaviorTreeTransition TransitionTo(BehaviorTreeNode child) =>
            tree.graph.Transitions.Single(transition => transition.destination == child);

        private void Index(BehaviorTreeNode child, int index) =>
            TransitionTo(child).SetTransitionIndex(index);

        private BehaviorTreeNode[] Order() => OrderUnder(parent);

        private BehaviorTreeNode[] OrderUnder(BehaviorTreeNode node) =>
            tree.graph.ChildTransitionsInPriorityOrder(node).Select(t => t.destination).ToArray();

        /// <summary>The stored indices, read in the order the children run.</summary>
        private int[] Indices() =>
            tree.graph.ChildTransitionsInPriorityOrder(parent).Select(t => t.TransitionIndex).ToArray();

        [Test]
        public void TheReportedBug_AChildDeletedAndReplacedInTheSameSpot_TakesTheSamePriority()
        {
            DeleteOnCanvas(left);

            // What releasing a drag writes. Placing the replacement is a drag, and it is what turned the
            // leftover gap into a clean order the new wire was then appended to.
            Index(middle, 0);
            Index(right, 1);

            var replacement = ChildAt(100.0f);
            ConnectOnCanvas(parent, replacement);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { replacement, middle, right }),
                "the leftmost child is the first one tried, from the moment it is connected");
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void AChildConnectedLeftOfItsSiblings_IsFirst()
        {
            var added = ChildAt(0.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { added, left, middle, right }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void AChildConnectedBetweenItsSiblings_IsSlottedBetweenThem()
        {
            var added = ChildAt(250.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, added, right }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void AChildConnectedRightOfItsSiblings_IsLast()
        {
            var added = ChildAt(400.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, right, added }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void TheFirstChildOfAParent_TakesIndexZero()
        {
            var lonely = BehaviorTreeAuthoring.AddNode<Selector>(tree, 800.0f, 0.0f);
            var only = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 800.0f, 200.0f);

            ConnectOnCanvas(lonely, only);

            Assert.That(TransitionTo(only).TransitionIndex, Is.EqualTo(0));
            Assert.That(OrderUnder(lonely), Is.EqualTo(new BehaviorTreeNode[] { only }));
        }

        [Test]
        public void Connecting_LeavesTheOtherChildrenInTheOrderTheyRanIn()
        {
            // Runs right, middle, left: the reverse of the layout, as a tree generated from code can be.
            Index(left, 2);
            Index(middle, 1);
            Index(right, 0);

            var added = ChildAt(0.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { added, right, middle, left }),
                "the new child takes the slot its own position counts to; connecting is not a reorder of " +
                "the children that were already there");
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void Connecting_WhereNoOrderWasRecorded_RecordsTheOneThatWasRunning()
        {
            // Every asset saved before indices were maintained: all zero, so position decides.
            Index(left, 0);
            Index(middle, 0);
            Index(right, 0);

            var added = ChildAt(150.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, added, middle, right }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void Connecting_WhereTheIndicesAreNotAnOrdering_DoesNotStartTrustingThem()
        {
            // What the old append left behind after a delete: a duplicate and a gap. Position was deciding,
            // and reading these as an order would run middle before left.
            Index(left, 2);
            Index(middle, 1);
            Index(right, 2);

            var added = ChildAt(400.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, right, added }),
                "the canvas has to read the children in the order the runtime was running them, or " +
                "connecting one child silently reorders the rest");
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void DeletingAChild_ClosesTheGapItLeaves()
        {
            DeleteOnCanvas(middle);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, right }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void DeletingAChild_LeavesTheOthersInTheOrderTheyRanIn()
        {
            Index(left, 2);
            Index(middle, 1);
            Index(right, 0);

            DeleteOnCanvas(middle);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { right, left }),
                "a gap reads as no order recorded and falls back to position, which would flip the two " +
                "children that were not touched");
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void DeletingAConnection_RenumbersTheSameWay()
        {
            Index(left, 2);
            Index(middle, 1);
            Index(right, 0);

            DeleteOnCanvas(TransitionTo(middle));

            Assert.That(tree.graph.elements.Contains(middle), Is.True, "only the wire was deleted");
            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { right, left }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void Deleting_WhereNoOrderWasRecorded_RecordsTheOneThatWasRunning()
        {
            Index(left, 0);
            Index(middle, 0);
            Index(right, 0);

            DeleteOnCanvas(left);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { middle, right }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void DeletingTheParent_RemovesItsWiresAndLeavesTheChildren()
        {
            DeleteOnCanvas(parent);

            Assert.That(tree.graph.Transitions.Any(transition => transition.source == parent), Is.False);
            Assert.That(tree.graph.elements.Contains(left), Is.True);
        }
    }
}

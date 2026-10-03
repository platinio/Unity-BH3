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
        private void DeleteOnCanvas(params GraphCore.IGraphElement[] elements)
        {
            canvas.selection.Clear();
            foreach (var element in elements) canvas.selection.Add(element);
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
        private int[] Indices() => IndicesUnder(parent);

        private int[] IndicesUnder(BehaviorTreeNode node) =>
            tree.graph.ChildTransitionsInPriorityOrder(node).Select(t => t.TransitionIndex).ToArray();

        /// <summary>A fourth child on the right, with all four running right to left: the reverse of the layout.</summary>
        private WaitTime AddAFourthChildAndReverseTheOrder()
        {
            var far = ChildAt(400.0f);
            BehaviorTreeAuthoring.Connect(tree, parent, far);

            Index(left, 3);
            Index(middle, 2);
            Index(right, 1);
            Index(far, 0);

            return far;
        }

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
        public void Connecting_WhereTheOrderDisagreesWithTheLayout_CountsTheSiblingsToTheLeft()
        {
            Index(left, 2);
            Index(middle, 1);
            Index(right, 0);

            var added = ChildAt(250.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { right, middle, added, left }),
                "two siblings sit to its left, so it is third; going in front of the first sibling to its " +
                "right would have made it first");
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void AChildConnectedAtASiblingsPosition_GoesAfterThatSibling()
        {
            var added = ChildAt(200.0f);
            ConnectOnCanvas(parent, added);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, added, right }));
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
        public void DeletingSeveralChildrenAtOnce_LeavesTheOthersInTheOrderTheyRanIn()
        {
            var far = AddAFourthChildAndReverseTheOrder();

            DeleteOnCanvas(middle, right);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { far, left }),
                "every wire that went has to be counted to recover the order, and the parent renumbered once");
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void DeletingSeveralConnectionsAtOnce_LeavesTheOthersInTheOrderTheyRanIn()
        {
            var far = AddAFourthChildAndReverseTheOrder();

            DeleteOnCanvas(TransitionTo(middle), TransitionTo(right));

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { far, left }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void DeletingAChildThatHasChildrenOfItsOwn_OnlyRenumbersItsSiblings()
        {
            var branch = BehaviorTreeAuthoring.AddNode<Selector>(tree, 250.0f, 200.0f);
            var first = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 200.0f, 400.0f);
            var second = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 300.0f, 400.0f);
            BehaviorTreeAuthoring.Connect(tree, parent, branch);
            BehaviorTreeAuthoring.Connect(tree, branch, first);
            BehaviorTreeAuthoring.Connect(tree, branch, second);

            Index(left, 3);
            Index(middle, 2);
            Index(branch, 1);
            Index(right, 0);

            DeleteOnCanvas(branch);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { right, middle, left }),
                "the deleted branch's own wires go in the same pass and are not this parent's children");
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(tree.graph.Transitions.Any(transition => transition.source == branch), Is.False);
        }

        [Test]
        public void DeletingConnectionsUnderTwoParentsAtOnce_RenumbersEachParent()
        {
            var other = BehaviorTreeAuthoring.AddNode<Selector>(tree, 700.0f, 0.0f);
            var otherLeft = ChildAt(600.0f);
            var otherMiddle = ChildAt(700.0f);
            var otherRight = ChildAt(800.0f);
            BehaviorTreeAuthoring.Connect(tree, other, otherRight);
            BehaviorTreeAuthoring.Connect(tree, other, otherMiddle);
            BehaviorTreeAuthoring.Connect(tree, other, otherLeft);

            Index(left, 2);
            Index(middle, 1);
            Index(right, 0);

            DeleteOnCanvas(TransitionTo(middle), TransitionTo(otherMiddle));

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { right, left }));
            Assert.That(Indices(), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(OrderUnder(other), Is.EqualTo(new BehaviorTreeNode[] { otherRight, otherLeft }));
            Assert.That(IndicesUnder(other), Is.EqualTo(new[] { 0, 1 }));
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

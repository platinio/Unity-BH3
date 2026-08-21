using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The order a composite tries its children in: by recorded transition index when those indices form a
    /// real ordering, and by left-to-right position when they do not.
    ///
    /// <para>
    /// "A real ordering" means exactly <c>0..n-1</c>, each once. A gap says something was removed without
    /// renumbering and a duplicate says two children claim one priority; in both cases the recorded order is
    /// not trustworthy, and where the designer put the nodes is the better answer.
    /// </para>
    ///
    /// <para>
    /// This rule had no EditMode coverage, which mattered because the priority badge asks it for every node
    /// on every repaint and the duplicate check was rewritten to stop allocating a scratch array per call.
    /// The cases below are the ones that distinguish the two branches — each arranges the children so that
    /// index order and position order disagree, so a test cannot pass by accident.
    /// </para>
    /// </summary>
    public class PriorityOrderTests
    {
        private const string Folder = "Assets/__PriorityOrderTests";

        private BehaviorTreeGraphAsset tree;
        private Sequence parent;
        private WaitTime left, middle, right;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__PriorityOrderTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");

            parent = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);

            // Laid out left to right, so "position order" is left, middle, right and is distinguishable from
            // any index order the tests set below.
            left = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 200.0f);
            middle = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 100.0f, 200.0f);
            right = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 200.0f, 200.0f);

            BehaviorTreeAuthoring.Connect(tree, parent, left);
            BehaviorTreeAuthoring.Connect(tree, parent, middle);
            BehaviorTreeAuthoring.Connect(tree, parent, right);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        private BehaviorTreeTransition TransitionTo(BehaviorTreeNode child) =>
            tree.graph.Transitions.Single(transition => transition.destination == child);

        private void Index(BehaviorTreeNode child, int index) =>
            TransitionTo(child).SetTransitionIndex(index);

        private BehaviorTreeNode[] Order() =>
            tree.graph.ChildTransitionsInPriorityOrder(parent).Select(t => t.destination).ToArray();

        [Test]
        public void ContiguousIndices_OrderByIndexRatherThanPosition()
        {
            Index(left, 2);
            Index(middle, 0);
            Index(right, 1);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { middle, right, left }),
                "0..n-1 each once is a real ordering, and tidying a layout must not change what runs first");
        }

        [Test]
        public void ADuplicateIndex_FallsBackToPosition()
        {
            Index(left, 1);
            Index(middle, 1);
            Index(right, 0);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, right }),
                "two children claiming one priority is not an ordering anybody chose");
        }

        [Test]
        public void AGapInTheIndices_FallsBackToPosition()
        {
            Index(left, 0);
            Index(middle, 1);
            Index(right, 3);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, right }),
                "a gap means something was removed without renumbering, so the rest cannot be trusted either");
        }

        [Test]
        public void ANegativeIndex_FallsBackToPosition()
        {
            Index(left, -1);
            Index(middle, 0);
            Index(right, 1);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, right }));
        }

        [Test]
        public void AnIndexPastTheChildCount_FallsBackToPosition()
        {
            Index(left, 0);
            Index(middle, 1);
            Index(right, 99);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, right }));
        }

        [Test]
        public void TheLastIndexBeingADuplicateOfTheFirst_IsStillCaught()
        {
            // The duplicate is found by comparing each index against the ones before it, so the case that
            // would slip through a scan that stopped early is the pair furthest apart.
            Index(left, 0);
            Index(middle, 1);
            Index(right, 0);

            Assert.That(Order(), Is.EqualTo(new BehaviorTreeNode[] { left, middle, right }));
        }

        [Test]
        public void OneChild_IsAnOrderingOfOne()
        {
            var only = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 500.0f, 0.0f);
            var child = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 500.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(tree, only, child);

            Assert.That(tree.graph.ChildTransitionsInPriorityOrder(only).Single().destination, Is.SameAs(child));
        }
    }
}

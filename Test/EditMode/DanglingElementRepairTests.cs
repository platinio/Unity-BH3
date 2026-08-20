using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Deleting a node leaves transitions and guards pointing at nothing, and something has to clear them up.
    ///
    /// <para>
    /// That used to happen in two places that disagreed. The canvas repair removed ownerless guards with an
    /// undo record; the transition <em>layout</em> pass removed transitions from inside a bare <c>catch</c>,
    /// with no undo record and on any exception at all. These pin the single rule both now ask, so a repair
    /// cannot drift back into a place that edits the graph while drawing it.
    /// </para>
    /// </summary>
    public class DanglingElementRepairTests
    {
        private const string Folder = "Assets/__DanglingElementTests";

        private BehaviorTreeGraphAsset tree;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__DanglingElementTests");
            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        private BehaviorTreeTransition OnlyTransition => tree.graph.Transitions.Single();

        // ------------------------------------------------------------------ transitions

        [Test]
        public void ATransitionWithBothEndsInTheGraph_IsNotDangling()
        {
            var parent = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            var child = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(tree, parent, child);

            Assert.That(BehaviorTreeCanvas.IsDangling(tree.graph, OnlyTransition), Is.False,
                "a transition between two live nodes is the normal case and must never be culled");
        }

        [Test]
        public void ATransitionWhoseDestinationWasDeleted_IsDangling()
        {
            var parent = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            var child = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(tree, parent, child);

            var transition = OnlyTransition;
            tree.graph.elements.Remove(child);

            Assert.That(BehaviorTreeCanvas.IsDangling(tree.graph, transition), Is.True);
        }

        [Test]
        public void ATransitionWhoseSourceWasDeleted_IsDangling()
        {
            var parent = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            var child = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(tree, parent, child);

            var transition = OnlyTransition;
            tree.graph.elements.Remove(parent);

            Assert.That(BehaviorTreeCanvas.IsDangling(tree.graph, transition), Is.True);
        }

        // ------------------------------------------------------------------ guards, unchanged behaviour

        [Test]
        public void AGuardWithALiveOwner_IsNotDangling()
        {
            var owner = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            var guard = BehaviorTreeAuthoring.GuardOnVariable(
                tree, owner, "hasTarget", true, false, -200.0f, 0.0f);

            Assert.That(BehaviorTreeCanvas.IsDangling(tree.graph, guard), Is.False);
        }

        [Test]
        public void AGuardWhoseOwnerWasDeleted_IsDangling()
        {
            var owner = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            var guard = BehaviorTreeAuthoring.GuardOnVariable(
                tree, owner, "hasTarget", true, false, -200.0f, 0.0f);

            tree.graph.elements.Remove(owner);

            Assert.That(BehaviorTreeCanvas.IsDangling(tree.graph, guard), Is.True,
                "the guard case is what the repair already handled, and must keep working");
        }

        // ------------------------------------------------------------------ everything else

        [Test]
        public void AnOrdinaryNode_IsNeverDangling()
        {
            var node = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            Assert.That(BehaviorTreeCanvas.IsDangling(tree.graph, node), Is.False,
                "the rule is about elements anchored to another element, not about every element");
        }
    }
}

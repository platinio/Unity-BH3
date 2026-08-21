using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Which guards belong to which node, and — the half that matters — that the answer stops being right
    /// the moment the graph changes.
    ///
    /// <para>
    /// This used to be a filter over every element in the graph, recomputed at every call. Correct by
    /// construction, and far too slow for the rate the canvas asks it at. Caching it buys the speed and
    /// takes on the only risk a cache has, so these cover the staleness cases rather than only the happy
    /// one: a guard added, a guard removed, and an owner that must not inherit somebody else's guards.
    /// </para>
    /// </summary>
    public class GuardIndexTests
    {
        private const string Folder = "Assets/__GuardIndexTests";

        private BehaviorTreeGraphAsset tree;
        private Sequence owner;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__GuardIndexTests");

            GuardIndex.Invalidate();

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            owner = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
        }

        [TearDown]
        public void TearDown()
        {
            GuardIndex.Invalidate();
            AssetDatabase.DeleteAsset(Folder);
        }

        private ConditionalExecution Guard(BehaviorTreeNode on, string variable, float y) =>
            BehaviorTreeAuthoring.GuardOnVariable(tree, on, variable, true, false, -200.0f, y);

        [Test]
        public void ANodeWithNoGuards_HasNone()
        {
            Assert.That(GuardIndex.Of(owner), Is.Empty);
        }

        [Test]
        public void ANullOwner_HasNone()
        {
            Assert.That(GuardIndex.Of(null), Is.Empty,
                "the drawer asks about whatever a transition points at, which can be nothing mid-edit");
        }

        [Test]
        public void AGuard_IsListedUnderItsOwner()
        {
            var guard = Guard(owner, "hasTarget", 0.0f);

            Assert.That(GuardIndex.Of(owner), Is.EqualTo(new[] { guard }));
        }

        [Test]
        public void SeveralGuards_ComeBackInGraphOrder()
        {
            var first = Guard(owner, "first", 0.0f);
            var second = Guard(owner, "second", 100.0f);
            var third = Guard(owner, "third", 200.0f);

            Assert.That(GuardIndex.Of(owner), Is.EqualTo(new[] { first, second, third }),
                "graph order is what GetConditionalIndex numbers guards in, so the drawn stack and the index " +
                "everything else quotes have to come from the same order");
        }

        [Test]
        public void AGuardAddedAfterTheFirstRead_IsSeen()
        {
            Assert.That(GuardIndex.Of(owner), Is.Empty);

            var added = Guard(owner, "added", 0.0f);

            Assert.That(GuardIndex.Of(owner), Is.EqualTo(new[] { added }),
                "a guard that the index cannot see is a guard the stack does not make room for");
        }

        [Test]
        public void AGuardRemovedAfterTheFirstRead_IsGone()
        {
            var guard = Guard(owner, "doomed", 0.0f);
            Assert.That(GuardIndex.Of(owner), Is.Not.Empty);

            tree.graph.elements.Remove(guard);

            Assert.That(GuardIndex.Of(owner), Is.Empty,
                "a deleted guard still counted would leave a gap in the stack above the node");
        }

        [Test]
        public void GuardsOfAnotherNode_AreNotListedHere()
        {
            var other = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 400.0f, 0.0f);

            var mine = Guard(owner, "mine", 0.0f);
            Guard(other, "theirs", 0.0f);

            Assert.That(GuardIndex.Of(owner), Is.EqualTo(new[] { mine }));
            Assert.That(GuardIndex.Of(other).Single().Owner, Is.SameAs(other));
        }

        [Test]
        public void AGuardWhoseOwnerWasDeleted_IsNotListedUnderAnyoneElse()
        {
            var orphaned = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 400.0f, 0.0f);
            Guard(orphaned, "orphan", 0.0f);

            tree.graph.elements.Remove(orphaned);

            Assert.That(GuardIndex.Of(owner), Is.Empty,
                "a dangling guard belongs to the canvas repair, not to some other node's stack");
        }

        // Not covered here: that ConditionalExecutionWidget.StackHeightAbove follows the index, which is the
        // consequence it exists to serve. Naming its ICanvas parameter from a test needs a
        // Unity.VisualScripting.Core.Editor reference the test assembly does not carry on this branch --
        // Unity-BH3#61 adds it. Worth adding here once that has merged; the staleness cases above are the
        // part that could actually be got wrong.
    }
}

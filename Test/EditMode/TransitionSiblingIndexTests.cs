using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Transitions running between the same two nodes are drawn spread apart rather than on top of each
    /// other, and working out the spread means knowing the whole set and this one's place in it.
    ///
    /// <para>
    /// That set used to be found by walking every transition in the graph, once per transition per repaint.
    /// These pin the two things the grouping has to get right for the drawing to come out the same: that
    /// direction is <em>not</em> part of the grouping — A→B and B→A must land together, or the pair they
    /// were spread apart to separate would draw on top of each other — and that graph order is preserved,
    /// because the spread offset is a running sum over the siblings before this one, so a reordered group
    /// moves wires nobody touched.
    /// </para>
    /// </summary>
    public class TransitionSiblingIndexTests
    {
        private const string Folder = "Assets/__TransitionSiblingIndexTests";

        private BehaviorTreeGraphAsset tree;
        private Sequence a, b, c;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__TransitionSiblingIndexTests");

            TransitionSiblingIndex.Invalidate();

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            a = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            b = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 200.0f);
            c = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 300.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            TransitionSiblingIndex.Invalidate();
            AssetDatabase.DeleteAsset(Folder);
        }

        private void Connect(BehaviorTreeNode from, BehaviorTreeNode to) =>
            BehaviorTreeAuthoring.Connect(tree, from, to);

        private BehaviorTreeTransition[] Between(BehaviorTreeNode from, BehaviorTreeNode to) =>
            TransitionSiblingIndex.Of(tree.graph, from, to).ToArray();

        private BehaviorTreeTransition[] AllTransitions => tree.graph.Transitions.ToArray();

        [Test]
        public void ALoneTransition_IsItsOwnOnlySibling()
        {
            Connect(a, b);

            Assert.That(Between(a, b), Is.EqualTo(AllTransitions),
                "the set always includes the asking transition; its own index in it is what positions it");
        }

        [Test]
        public void TwoTransitionsBetweenTheSamePair_AreSiblings()
        {
            Connect(a, b);
            Connect(a, b);

            Assert.That(Between(a, b).Length, Is.EqualTo(2));
        }

        [Test]
        public void AnOppositeTransition_IsASibling()
        {
            Connect(a, b);
            Connect(b, a);

            Assert.That(Between(a, b).Length, Is.EqualTo(2),
                "the spread exists so A->B and B->A do not draw on top of each other, so they have to be " +
                "grouped together -- direction must not be part of the key");
        }

        [Test]
        public void AskingFromEitherEnd_GivesTheSameGroup()
        {
            Connect(a, b);
            Connect(b, a);

            Assert.That(Between(a, b), Is.EqualTo(Between(b, a)));
        }

        [Test]
        public void ATransitionToADifferentNode_IsNotASibling()
        {
            Connect(a, b);
            Connect(a, c);

            Assert.That(Between(a, b).Length, Is.EqualTo(1));
            Assert.That(Between(a, c).Length, Is.EqualTo(1));
        }

        [Test]
        public void SiblingsComeBackInGraphOrder()
        {
            Connect(a, b);
            Connect(b, a);
            Connect(a, b);

            // graph.Transitions order is the order they were added, and the spread sums the siblings before
            // this one, so any other order moves wires.
            var expected = AllTransitions.Where(t =>
                (t.source == a && t.destination == b) || (t.source == b && t.destination == a)).ToArray();

            Assert.That(Between(a, b), Is.EqualTo(expected));
        }

        [Test]
        public void ASelfTransition_Groups()
        {
            Connect(a, a);

            Assert.That(Between(a, a).Length, Is.EqualTo(1),
                "both ends being the same node falls out of an unordered key without a special case");
        }

        [Test]
        public void ATransitionAddedAfterTheFirstRead_IsSeen()
        {
            Connect(a, b);
            Assert.That(Between(a, b).Length, Is.EqualTo(1));

            Connect(a, b);

            Assert.That(Between(a, b).Length, Is.EqualTo(2),
                "a sibling the index cannot see is a wire drawn on top of another one");
        }

        [Test]
        public void ATransitionRemovedAfterTheFirstRead_IsGone()
        {
            Connect(a, b);
            Connect(a, b);
            Assert.That(Between(a, b).Length, Is.EqualTo(2));

            tree.graph.elements.Remove(AllTransitions[0]);

            Assert.That(Between(a, b).Length, Is.EqualTo(1),
                "a deleted sibling still counted leaves the survivor spread off to one side");
        }

        [Test]
        public void AMissingEndpoint_HasNoSiblings()
        {
            Connect(a, b);

            Assert.That(TransitionSiblingIndex.Of(tree.graph, a, null), Is.Empty);
            Assert.That(TransitionSiblingIndex.Of(null, a, b), Is.Empty);
        }
    }
}

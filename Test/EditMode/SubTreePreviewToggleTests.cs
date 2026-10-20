using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Where the collapse button lands on a node, and what it answers to a click there. The button is drawn
    /// and hit-tested from the same rect, so these pin both at once.
    /// </summary>
    public class SubTreePreviewToggleTests
    {
        private static readonly Rect CollapsedNode = new Rect(400.0f, 250.0f, 150.0f, 100.0f);

        [Test]
        public void TheButtonSitsInsideTheNode()
        {
            var button = SubTreePreviewToggle.ButtonOn(CollapsedNode);

            Assert.That(CollapsedNode.Contains(button.min), Is.True);
            Assert.That(CollapsedNode.Contains(button.max), Is.True);
        }

        [Test]
        public void TheButtonIsInTheNodesLeftHalfAndTopHalf()
        {
            var button = SubTreePreviewToggle.ButtonOn(CollapsedNode);

            Assert.That(button.center.x, Is.LessThan(CollapsedNode.center.x));
            Assert.That(button.center.y, Is.LessThan(CollapsedNode.center.y));
        }

        [Test]
        public void TheButtonNeverLandsOnTheProblemBadge()
        {
            var button = SubTreePreviewToggle.ButtonOn(CollapsedNode);
            var badge = BehaviorTreeNodeElementWidget.ProblemBadgeOn(CollapsedNode);

            Assert.That(button.Overlaps(badge), Is.False,
                "a node that is both collapsed and in trouble draws the two markers on top of each other");
        }

        [Test]
        public void TheButtonFollowsTheNode()
        {
            var moved = new Rect(CollapsedNode.position + new Vector2(90.0f, 40.0f), CollapsedNode.size);

            Assert.That(SubTreePreviewToggle.ButtonOn(moved).position - SubTreePreviewToggle.ButtonOn(CollapsedNode).position,
                Is.EqualTo(new Vector2(90.0f, 40.0f)));
        }

        [Test]
        public void AClickOnTheButtonIsAHit()
        {
            var node = NodeRunningABranch();

            Assert.That(SubTreePreviewToggle.IsHit(CollapsedNode, node, SubTreePreviewToggle.ButtonOn(CollapsedNode).center), Is.True);
        }

        [Test]
        public void AClickOnTheRestOfTheNodeIsNotAHit()
        {
            var node = NodeRunningABranch();

            Assert.That(SubTreePreviewToggle.IsHit(CollapsedNode, node, CollapsedNode.center), Is.False);
        }

        [Test]
        public void AClickJustOutsideTheButtonIsNotAHit()
        {
            var node = NodeRunningABranch();
            var justPastTheCorner = SubTreePreviewToggle.ButtonOn(CollapsedNode).max + new Vector2(1.0f, 1.0f);

            Assert.That(SubTreePreviewToggle.IsHit(CollapsedNode, node, justPastTheCorner), Is.False);
        }

        [Test]
        public void ANodeWithNoBranchReferencedHasNoButtonToHit()
        {
            var runsNothing = new RunBehaviorTreeGraphNode();

            Assert.That(SubTreePreviewToggle.IsHit(CollapsedNode, runsNothing, SubTreePreviewToggle.ButtonOn(CollapsedNode).center),
                Is.False);
        }

        private static RunBehaviorTreeGraphNode NodeRunningABranch()
        {
            var node = new RunBehaviorTreeGraphNode();
            node.SetBehaviorTreeGraphAsset(ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>());

            return node;
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Where the collapse button lands on a node, what it answers to a click there, and which glyph it
    /// shows. The button is drawn and hit-tested from the same rect, so these pin both at once.
    /// </summary>
    public class SubTreePreviewToggleTests
    {
        private static readonly Rect NodeRect = new Rect(400.0f, 250.0f, 150.0f, 100.0f);

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyTheAssetsTheseTestsMade()
        {
            foreach (var asset in created) Object.DestroyImmediate(asset);
            created.Clear();
        }

        [Test]
        public void TheButtonSitsInsideTheNode()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);

            Assert.That(NodeRect.Contains(button.min), Is.True);
            Assert.That(NodeRect.Contains(button.max), Is.True);
        }

        [Test]
        public void TheButtonIsInTheNodesTopLeftCorner()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);

            Assert.That(button.center.x, Is.LessThan(NodeRect.center.x));
            Assert.That(button.center.y, Is.LessThan(NodeRect.center.y));
        }

        [Test]
        public void TheButtonNeverLandsOnTheProblemBadge()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);
            var badge = BehaviorTreeNodeElementWidget.ProblemBadgeOn(NodeRect);

            Assert.That(button.Overlaps(badge), Is.False,
                "a node that is both collapsed and in trouble draws the two markers on top of each other");
        }

        [Test]
        public void AClickOnTheButtonIsAHit()
        {
            var node = NodeRunningABranch();

            Assert.That(SubTreePreviewToggle.IsHit(NodeRect, node, SubTreePreviewToggle.ButtonOn(NodeRect).center), Is.True);
        }

        [Test]
        public void AClickOnTheRestOfTheNodeIsNotAHit()
        {
            var node = NodeRunningABranch();

            Assert.That(SubTreePreviewToggle.IsHit(NodeRect, node, NodeRect.center), Is.False);
        }

        [Test]
        public void AClickJustOutsideTheButtonIsNotAHit()
        {
            var node = NodeRunningABranch();
            var justPastTheCorner = SubTreePreviewToggle.ButtonOn(NodeRect).max + new Vector2(1.0f, 1.0f);

            Assert.That(SubTreePreviewToggle.IsHit(NodeRect, node, justPastTheCorner), Is.False);
        }

        [Test]
        public void ANodeWithNoBranchReferencedHasNoButtonToHit()
        {
            var runsNothing = new RunBehaviorTreeGraphNode();

            Assert.That(SubTreePreviewToggle.IsHit(NodeRect, runsNothing, SubTreePreviewToggle.ButtonOn(NodeRect).center),
                Is.False);
        }

        [Test]
        public void ACollapsedNodeOffersToExpand()
        {
            var node = NodeRunningABranch();
            node.IsSubTreePreviewCollapsed = true;

            Assert.That(SubTreePreviewToggle.ShowsPlusGlyph(node), Is.True);
        }

        [Test]
        public void ANodeShowingItsPreviewOffersToCollapse()
        {
            var node = NodeRunningABranch();

            Assert.That(SubTreePreviewToggle.ShowsPlusGlyph(node), Is.False);
        }

        private RunBehaviorTreeGraphNode NodeRunningABranch()
        {
            var branch = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            created.Add(branch);

            var node = new RunBehaviorTreeGraphNode();
            node.SetBehaviorTreeGraphAsset(branch);

            return node;
        }
    }
}

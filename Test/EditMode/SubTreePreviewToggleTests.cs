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

        /// <summary>
        /// Hanging across the top edge is what keeps the button off the node's contents: the port rows
        /// start a few pixels inside the box, and a collapsed node is almost nothing else.
        /// </summary>
        [Test]
        public void TheButtonHangsAcrossTheNodesTopEdge()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);

            Assert.That(button.yMin, Is.LessThan(NodeRect.yMin));
            Assert.That(button.yMax, Is.LessThan(NodeRect.center.y));
        }

        /// <summary>
        /// A node carries one marker per corner so that any combination of them can be on at once. Three
        /// of them used to pile into the top-left and overlap each other.
        /// </summary>
        [Test]
        public void NoTwoCornerMarkersOverlap()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);
            var badge = BehaviorTreeNodeElementWidget.ProblemBadgeOn(NodeRect);
            var executionStatus = BehaviorTreeNodeElementWidget.ExecutionStateIconOn(NodeRect);

            Assert.That(button.Overlaps(badge), Is.False, "collapse toggle over the problem badge");
            Assert.That(button.Overlaps(executionStatus), Is.False, "collapse toggle over the execution status");
            Assert.That(badge.Overlaps(executionStatus), Is.False, "problem badge over the execution status");
        }

        /// <summary>
        /// The dot is anchored a little left of the node's own edge, so the room the button claims has to
        /// cover that as well as the button.
        /// </summary>
        [Test]
        public void TheRoomTheButtonClaimsLeavesTheBreakpointDotClear()
        {
            var node = NodeRunningABranch();
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);

            Assert.That(NodeRect.xMin + SubTreePreviewToggle.RoomTakenOn(node),
                Is.GreaterThan(button.xMax));
        }

        [Test]
        public void ANodeThatCannotCollapseClaimsNoneOfItsCorner()
        {
            Assert.That(SubTreePreviewToggle.RoomTakenOn(new WaitTime()), Is.Zero);
            Assert.That(SubTreePreviewToggle.RoomTakenOn(new RunBehaviorTreeGraphNode()), Is.Zero);
        }

        [Test]
        public void TheBottomMarkersHangBelowTheNodesName()
        {
            Assert.That(BehaviorTreeNodeElementWidget.ProblemBadgeOn(NodeRect).yMin,
                Is.GreaterThanOrEqualTo(BehaviorTreeNodeElementWidget.BelowTheNameOf(NodeRect)));
            Assert.That(BehaviorTreeNodeElementWidget.ExecutionStateIconOn(NodeRect).yMin,
                Is.GreaterThanOrEqualTo(BehaviorTreeNodeElementWidget.BelowTheNameOf(NodeRect)));
        }

        [Test]
        public void TheButtonTakesTheTopLeftCornerAndTheProblemBadgeTheOpposite()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);
            var badge = BehaviorTreeNodeElementWidget.ProblemBadgeOn(NodeRect);

            Assert.That(button.center.x, Is.LessThan(NodeRect.center.x));
            Assert.That(button.center.y, Is.LessThan(NodeRect.center.y));
            Assert.That(badge.center.x, Is.GreaterThan(NodeRect.center.x));
            Assert.That(badge.center.y, Is.GreaterThan(NodeRect.center.y));
        }

        [Test]
        public void TheButtonIsCentredOnTheNodesTopLeftCorner()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);

            Assert.That(button.center.x, Is.EqualTo(NodeRect.xMin).Within(0.001f));
            Assert.That(button.center.y, Is.EqualTo(NodeRect.yMin).Within(0.001f));
        }

        [Test]
        public void TheRoomTheButtonNeedsCoversTheButton()
        {
            var button = SubTreePreviewToggle.ButtonOn(NodeRect);

            Assert.That(NodeRect.xMin + SubTreePreviewToggle.RoomNeededAtTheCorner,
                Is.GreaterThanOrEqualTo(button.xMax),
                "a header drawn beside the button would start underneath it");
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

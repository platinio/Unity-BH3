using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A caller that runs a three-node branch, which is the case the collapse toggle exists for: the node
    /// is drawn as a frame around the whole branch until somebody collapses it.
    /// </summary>
    public class SubTreePreviewCollapseTests
    {
        private const string Folder = "Assets/__SubTreePreviewCollapseTests";
        private const string BranchPath = Folder + "/Branch.asset";
        private const string CallerPath = Folder + "/Caller.asset";

        private BehaviorTreeGraphAsset branch;
        private BehaviorTreeGraphAsset caller;
        private RunBehaviorTreeGraphNode runsTheBranch;

        [SetUp]
        public void BuildACallerAndTheBranchItRuns()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "__SubTreePreviewCollapseTests");
            }

            branch = BehaviorTreeAuthoring.CreateTree(BranchPath);
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(branch, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(branch, branch.graph.EntryNode, sequence);
            foreach (var _ in Enumerable.Range(0, 3))
            {
                BehaviorTreeAuthoring.Connect(branch, sequence, BehaviorTreeAuthoring.AddNode<WaitTime>(branch, 0.0f, 0.0f));
            }
            BehaviorTreeAuthoring.Save(branch);
            BehaviorTreeLayout.Arrange(branch);

            caller = BehaviorTreeAuthoring.CreateTree(CallerPath);
            var selector = BehaviorTreeAuthoring.AddNode<Selector>(caller, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, caller.graph.EntryNode, selector);

            runsTheBranch = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, selector, runsTheBranch);
            BehaviorTreeAuthoring.Connect(caller, selector, BehaviorTreeAuthoring.AddNode<WaitTime>(caller, 0.0f, 0.0f));
            BehaviorTreeAuthoring.Save(caller);
        }

        [TearDown]
        public void DeleteTheTrees()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void ABranchNodeShowsItsPreviewUntilItIsCollapsed()
        {
            Assert.That(runsTheBranch.DrawsSubTreePreview, Is.True);

            runsTheBranch.IsSubTreePreviewCollapsed = true;

            Assert.That(runsTheBranch.DrawsSubTreePreview, Is.False);
        }

        [Test]
        public void ANodeWithNoBranchReferencedShowsNoPreview()
        {
            var runsNothing = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(caller, 0.0f, 0.0f);

            Assert.That(runsNothing.DrawsSubTreePreview, Is.False);
        }

        [Test]
        public void AnExpandedBranchNodeIsMeasuredAsAFrameAroundTheWholeBranch()
        {
            Assert.That(SubTreeFootprint.DrawnAs(runsTheBranch).width,
                Is.GreaterThan(SubTreeFootprint.EmptyNodeSize.x),
                "a node that runs a three-node branch is measured as the box an empty one is created at");
        }

        [Test]
        public void ACollapsedBranchNodeIsMeasuredAsTheBoxItWasCreatedAt()
        {
            runsTheBranch.IsSubTreePreviewCollapsed = true;

            Assert.That(SubTreeFootprint.DrawnAs(runsTheBranch).size, Is.EqualTo(SubTreeFootprint.EmptyNodeSize));
        }

        [Test]
        public void CollapsingLeavesTheBranchNodeWhereItAlreadySits()
        {
            BehaviorTreeLayout.Arrange(caller);
            var wasAt = runsTheBranch.Position.position;

            runsTheBranch.IsSubTreePreviewCollapsed = true;

            Assert.That(SubTreeFootprint.DrawnAs(runsTheBranch).position, Is.EqualTo(wasAt));
        }

        [Test]
        public void AnExpandedBranchNodeReservesTheFramesHeaderBandAboveIt()
        {
            Assert.That(SubTreeFootprint.RoomAbove(runsTheBranch),
                Is.EqualTo(SubTreeFootprint.GroupHeaderLift).Within(0.001f));
        }

        [Test]
        public void ACollapsedBranchNodeReservesNoHeaderBandAboveIt()
        {
            runsTheBranch.IsSubTreePreviewCollapsed = true;

            Assert.That(SubTreeFootprint.RoomAbove(runsTheBranch), Is.EqualTo(0.0f).Within(0.001f));
        }

        [Test]
        public void ANodeWithNoBranchReferencedReservesNoHeaderBandAboveIt()
        {
            var runsNothing = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(caller, 0.0f, 0.0f);

            Assert.That(SubTreeFootprint.RoomAbove(runsNothing), Is.EqualTo(0.0f).Within(0.001f));
        }

        [Test]
        public void ArrangingTheTreeLeavesACollapsedBranchNodeCollapsed()
        {
            runsTheBranch.IsSubTreePreviewCollapsed = true;

            BehaviorTreeLayout.Arrange(caller);

            Assert.That(runsTheBranch.Position.size, Is.EqualTo(SubTreeFootprint.EmptyNodeSize));
        }

        [Test]
        public void CollapsingABranchNarrowsTheCanvasTheTreeIsArrangedInto()
        {
            BehaviorTreeLayout.Arrange(caller);
            var expandedWidth = WidthOfTheArrangedCaller();

            runsTheBranch.IsSubTreePreviewCollapsed = true;
            BehaviorTreeLayout.Arrange(caller);

            Assert.That(WidthOfTheArrangedCaller(), Is.LessThan(expandedWidth));
        }

        [Test]
        public void TheCollapsedBranchIsStillCollapsedWhenTheTreeIsOpenedAgain()
        {
            runsTheBranch.IsSubTreePreviewCollapsed = true;
            BehaviorTreeAuthoring.Save(caller);

            var reopened = Reload().graph.Nodes.OfType<RunBehaviorTreeGraphNode>().Single();

            Assert.That(reopened.IsSubTreePreviewCollapsed, Is.True);
        }

        [Test]
        public void AnExpandedBranchIsStillExpandedWhenTheTreeIsOpenedAgain()
        {
            BehaviorTreeAuthoring.Save(caller);

            var reopened = Reload().graph.Nodes.OfType<RunBehaviorTreeGraphNode>().Single();

            Assert.That(reopened.IsSubTreePreviewCollapsed, Is.False);
        }

        private float WidthOfTheArrangedCaller()
        {
            var nodes = caller.graph.Nodes.ToList();

            return nodes.Max(node => node.Position.xMax) - nodes.Min(node => node.Position.xMin);
        }

        /// <summary>
        /// Reads the saved file back as a genuinely fresh deserialization.
        ///
        /// <para>
        /// Through a copy rather than by reimporting in place: <c>LoadAssetAtPath</c> hands back the instance
        /// already in memory, so reimporting proves nothing about what was written. A copy is loaded from the
        /// bytes on disk — the same thing a fresh checkout sees.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset Reload()
        {
            var copyPath = Folder + "/Reopened.asset";

            AssetDatabase.DeleteAsset(copyPath);
            AssetDatabase.CopyAsset(CallerPath, copyPath);
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(copyPath);
        }
    }
}

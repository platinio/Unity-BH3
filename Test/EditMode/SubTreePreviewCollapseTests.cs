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
        public void ACollapsedBranchNodeIsNeverSmallerThanTheParametersItDeclares()
        {
            BehaviorTreeAuthoring.Declare(branch, "attackRange", 0.0f, BehaviorTreeAuthoring.DeclarationScope.Required);
            BehaviorTreeAuthoring.Declare(branch, "idleTime", 3.5f, BehaviorTreeAuthoring.DeclarationScope.Optional);
            BehaviorTreeAuthoring.Save(branch);
            runsTheBranch.RefreshParameters();
            runsTheBranch.IsSubTreePreviewCollapsed = true;

            var collapsed = SubTreeFootprint.DrawnAs(runsTheBranch);

            Assert.That(collapsed.size, Is.EqualTo(ContractPortLayout.SizeForPorts(runsTheBranch)),
                "the collapsed box reports a size the node is not drawn at, so the layout will keep correcting it");
            Assert.That(collapsed.width, Is.GreaterThanOrEqualTo(SubTreeFootprint.EmptyNodeSize.x));
            Assert.That(collapsed.height, Is.GreaterThanOrEqualTo(SubTreeFootprint.EmptyNodeSize.y));
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

        /// <summary>
        /// The layout writes each sub-tree node the size it is drawn at. If that size is one the node is
        /// never actually drawn at, every run finds the same difference, rearranges the whole tree to correct
        /// it and saves -- throwing away a hand-made layout each time, silently.
        /// </summary>
        [Test]
        public void ArrangingACollapsedTreeTwiceLeavesItWhereTheFirstRunPutIt()
        {
            runsTheBranch.IsSubTreePreviewCollapsed = true;
            BehaviorTreeLayout.Arrange(caller);
            var arrangedAt = PositionsOfTheCaller();

            var secondRun = BehaviorTreeLayout.Arrange(caller);

            Assert.That(secondRun.Any(tree => tree.arranged), Is.False, "a tidy collapsed tree was rearranged");
            Assert.That(PositionsOfTheCaller(), Is.EqualTo(arrangedAt));
        }

        /// <summary>
        /// A guard is counted by the layout at its nominal height and drawn at its measured one, so the two
        /// disagree for any branch holding one. That difference is a height, and a height moves no siblings.
        /// </summary>
        [Test]
        public void ArrangingATreeWhoseBranchHoldsAGuardTwiceLeavesItWhereTheFirstRunPutIt()
        {
            var guarded = branch.graph.Nodes.OfType<WaitTime>().First();
            BehaviorTreeAuthoring.GuardOnVariable(branch, guarded, "isAlerted", true, false, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Save(branch);

            BehaviorTreeLayout.Arrange(caller);
            var arrangedAt = PositionsOfTheCaller();

            var secondRun = BehaviorTreeLayout.Arrange(caller);

            Assert.That(secondRun.Any(tree => tree.arranged), Is.False, "a tidy guarded tree was rearranged");
            Assert.That(PositionsOfTheCaller(), Is.EqualTo(arrangedAt));
        }

        /// <summary>
        /// Being collapsed is not by itself a reason to arrange a tree: one that was saved collapsed is
        /// already spaced for it. Arranging on the flag rather than on a stale width would rewrite every
        /// node's position, and save it, each time somebody opened such a tree.
        /// </summary>
        [Test]
        public void OpeningATreeThatWasSavedCollapsedDoesNotRearrangeIt()
        {
            runsTheBranch.IsSubTreePreviewCollapsed = true;
            BehaviorTreeLayout.Arrange(caller);
            BehaviorTreeAuthoring.Save(caller);

            var reopened = Reload();
            var arrangedAt = reopened.graph.Nodes.Select(node => node.Position).ToArray();

            var opening = BehaviorTreeLayout.Arrange(reopened);

            Assert.That(System.Linq.Enumerable.Any(opening, tree => tree.arranged), Is.False,
                "a tree that was saved collapsed was rearranged just for being collapsed");
            Assert.That(reopened.graph.Nodes.Select(node => node.Position).ToArray(), Is.EqualTo(arrangedAt));
        }

        /// <summary>
        /// The case that separates "the arrangement is stale" from "the branch is collapsed" with nothing
        /// else to fall back on. Pointing a node at a smaller branch narrows it, which leaves a hole and
        /// overlaps nothing -- so neither the overlap count nor the collapsed flag notices, and only the
        /// width does.
        /// </summary>
        [Test]
        public void PointingANodeAtASmallerBranchRearrangesTheTreeThatRunsIt()
        {
            BehaviorTreeLayout.Arrange(caller);

            var smaller = BehaviorTreeAuthoring.CreateTree(Folder + "/Smaller.asset");
            BehaviorTreeAuthoring.Connect(smaller, smaller.graph.EntryNode,
                BehaviorTreeAuthoring.AddNode<WaitTime>(smaller, 0.0f, 0.0f));
            BehaviorTreeAuthoring.Save(smaller);
            BehaviorTreeLayout.Arrange(smaller);

            runsTheBranch.SetBehaviorTreeGraphAsset(smaller);

            var afterShrinking = BehaviorTreeLayout.Arrange(caller);

            Assert.That(runsTheBranch.IsSubTreePreviewCollapsed, Is.False, "nothing here was collapsed");
            Assert.That(BehaviorTreeLayout.CountOverlappingPairs(caller.graph), Is.Zero,
                "a narrower node overlaps nothing, so the overlap count cannot be what catches this");
            Assert.That(System.Linq.Enumerable.Any(afterShrinking, tree => tree.arranged), Is.True,
                "the tree is still spaced around a width the branch no longer has");
        }

        /// <summary>
        /// The other direction: a branch that grew needs arranging again although nothing was collapsed.
        /// </summary>
        [Test]
        public void ABranchThatGrewRearrangesTheTreeThatRunsIt()
        {
            BehaviorTreeLayout.Arrange(caller);

            var sequence = branch.graph.Nodes.OfType<Sequence>().Single();
            foreach (var _ in Enumerable.Range(0, 4))
            {
                BehaviorTreeAuthoring.Connect(branch, sequence,
                    BehaviorTreeAuthoring.AddNode<WaitTime>(branch, 0.0f, 0.0f));
            }
            BehaviorTreeAuthoring.Save(branch);

            var afterGrowing = BehaviorTreeLayout.Arrange(caller);

            Assert.That(System.Linq.Enumerable.Any(afterGrowing, tree => tree.arranged), Is.True);
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
        public void CollapsingABranchLeavesItsSiblingsWhereTheyAreUntilTheTreeIsArranged()
        {
            BehaviorTreeLayout.Arrange(caller);
            var siblingWasAt = caller.graph.Nodes.OfType<WaitTime>().Single().Position.position;

            runsTheBranch.IsSubTreePreviewCollapsed = true;

            Assert.That(caller.graph.Nodes.OfType<WaitTime>().Single().Position.position, Is.EqualTo(siblingWasAt));
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

        private Rect[] PositionsOfTheCaller() =>
            caller.graph.Nodes.Select(node => node.Position).ToArray();

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

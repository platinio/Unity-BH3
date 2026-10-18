using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A caller and the branch it runs, both built with every node dropped at the same spot, which is what
    /// authoring from code produces before anybody says where anything goes.
    /// </summary>
    public class BehaviorTreeLayoutTests
    {
        private const string Folder = "Assets/__BehaviorTreeLayoutTests";
        private const string BranchPath = Folder + "/Branch.asset";
        private const string CallerPath = Folder + "/Caller.asset";

        private BehaviorTreeGraphAsset branch;
        private BehaviorTreeGraphAsset caller;
        private Selector selector;
        private RunBehaviorTreeGraphNode runsTheBranch;

        [SetUp]
        public void BuildTwoTreesWithEverythingOnTopOfEachOther()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__BehaviorTreeLayoutTests");

            branch = BehaviorTreeAuthoring.CreateTree(BranchPath);
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(branch, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(branch, branch.graph.EntryNode, sequence);
            foreach (var _ in Enumerable.Range(0, 3))
            {
                BehaviorTreeAuthoring.Connect(branch, sequence, BehaviorTreeAuthoring.AddNode<WaitTime>(branch, 0.0f, 0.0f));
            }
            BehaviorTreeAuthoring.Save(branch);

            caller = BehaviorTreeAuthoring.CreateTree(CallerPath);
            selector = BehaviorTreeAuthoring.AddNode<Selector>(caller, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, caller.graph.EntryNode, selector);

            runsTheBranch = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(caller, selector, runsTheBranch);
            BehaviorTreeAuthoring.Connect(caller, selector, BehaviorTreeAuthoring.AddNode<WaitTime>(caller, 0.0f, 0.0f));
            BehaviorTreeAuthoring.Connect(caller, selector, BehaviorTreeAuthoring.AddNode<WaitTime>(caller, 0.0f, 0.0f));
            BehaviorTreeAuthoring.Save(caller);
        }

        [TearDown]
        public void DeleteTheTrees()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void ArrangesATreeWhoseNodesAreDrawnOverEachOther()
        {
            Assert.That(BehaviorTreeLayout.CountOverlappingPairs(caller.graph), Is.GreaterThan(0),
                "the tree is supposed to start as a pile; nothing is being tested");

            BehaviorTreeLayout.Arrange(caller);

            Assert.That(BehaviorTreeLayout.CountOverlappingPairs(caller.graph), Is.Zero);
            Assert.That(BehaviorTreeLayout.CountOverlappingPairs(branch.graph), Is.Zero);
        }

        [Test]
        public void LeavesATreeNobodyIsDrawingOverWhereItIs()
        {
            BehaviorTreeLayout.Arrange(caller);
            var arrangedAt = PositionsOf(caller);

            var secondRun = BehaviorTreeLayout.Arrange(caller);

            Assert.That(secondRun.Any(tree => tree.arranged), Is.False, "a tidy tree was rearranged");
            Assert.That(PositionsOf(caller), Is.EqualTo(arrangedAt));
        }

        [Test]
        public void SizesTheCallersBranchNodeFromTheBranchItRuns()
        {
            BehaviorTreeLayout.Arrange(caller);

            var drawnAs = SubTreeFootprint.BoxAt(runsTheBranch.Position.position, SubTreeFootprint.BoundsOf(branch.graph));

            Assert.That(runsTheBranch.Position.size, Is.EqualTo(drawnAs.size));
            Assert.That(runsTheBranch.Position.width, Is.GreaterThan(SubTreeFootprint.EmptyNodeSize.x),
                "a node that runs a three-node branch is still the size an empty one is created at");
        }

        [Test]
        public void ArrangesABranchBeforeTheTreeThatRunsIt()
        {
            var arranged = BehaviorTreeLayout.Arrange(caller).Select(tree => tree.tree).ToList();

            Assert.That(arranged, Is.EqualTo(new[] { BranchPath, CallerPath }));
        }

        [Test]
        public void LaysSiblingsOutLeftToRightInPriorityOrder()
        {
            BehaviorTreeLayout.Arrange(caller);

            var children = caller.graph.ChildTransitionsInPriorityOrder(selector)
                .Select(transition => (BehaviorTreeNode)transition.destination)
                .ToList();

            Assert.That(children.Select(child => child.Position.xMin), Is.Ordered.Ascending);
        }

        [Test]
        public void LeavesAGenerationOfRoomBelowAParent()
        {
            BehaviorTreeLayout.Arrange(caller);

            foreach (var child in caller.graph.ChildTransitionsInPriorityOrder(selector).Select(t => (BehaviorTreeNode)t.destination))
            {
                Assert.That(child.Position.yMin - selector.Position.yMax,
                    Is.GreaterThanOrEqualTo(BehaviorTreeLayout.GapBetweenGenerations), child.NodeName + " sits too close");
            }
        }

        private static List<Rect> PositionsOf(BehaviorTreeGraphAsset tree) =>
            tree.graph.Nodes.Select(node => node.Position).ToList();
    }
}

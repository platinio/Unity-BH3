using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Runs the layout over a copy of the RTS Worker — a real tree, six branches deep, authored at
    /// coordinates chosen before anyone knew how wide a sub-tree is drawn. The copy is made fresh for each
    /// test and deleted afterwards, so the fixture stays as messy as it was committed.
    /// </summary>
    public class BehaviorTreeLayoutTests
    {
        private const string Fixtures = "Assets/ArcaneOnyx/BH3/Test/EditMode/Fixtures";
        private const string Fixture = Fixtures + "/MessyWorker";
        private const string UnderTestFolder = "MessyWorkerUnderTest";
        private const string UnderTest = Fixtures + "/" + UnderTestFolder;

        private BehaviorTreeGraphAsset worker;
        private List<BehaviorTreeGraphAsset> trees;

        [SetUp]
        public void CopyTheFixture()
        {
            DeleteTheCopy();
            AssetDatabase.CreateFolder(Fixtures, UnderTestFolder);

            var copyOf = new Dictionary<string, string>();

            foreach (var guid in AssetDatabase.FindAssets("t:BehaviorTreeGraphAsset", new[] { Fixture }))
            {
                var original = AssetDatabase.GUIDToAssetPath(guid);
                var copy = UnderTest + "/" + System.IO.Path.GetFileName(original);

                Assert.That(AssetDatabase.CopyAsset(original, copy), Is.True, "could not copy " + original);
                copyOf[original] = copy;
            }

            AssetDatabase.Refresh();

            var copiedFrom = copyOf.ToDictionary(
                pair => pair.Key, pair => AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(pair.Value));

            PointBranchesAtTheCopies(copiedFrom);

            trees = copiedFrom.Values.ToList();
            worker = trees.Single(tree => tree.name == "Worker");
        }

        [TearDown]
        public void DeleteTheCopy()
        {
            if (AssetDatabase.IsValidFolder(UnderTest)) AssetDatabase.DeleteAsset(UnderTest);
        }

        [Test]
        public void ArrangesEveryTreeWhoseNodesOverlap()
        {
            Assert.That(trees.Sum(tree => BehaviorTreeLayout.CountOverlappingPairs(tree.graph)), Is.GreaterThan(0),
                "the fixture is supposed to be a messy tree; nothing is being tested");

            BehaviorTreeLayout.Arrange(worker);

            foreach (var tree in trees)
            {
                Assert.That(BehaviorTreeLayout.CountOverlappingPairs(tree.graph), Is.Zero, tree.name + " still overlaps");
            }
        }

        [Test]
        public void LeavesATreeNobodyIsDrawingOverWhereItIs()
        {
            BehaviorTreeLayout.Arrange(worker);
            var arrangedAt = PositionsOf(worker);

            var secondRun = BehaviorTreeLayout.Arrange(worker);

            Assert.That(secondRun.Any(tree => tree.arranged), Is.False, "a tidy tree was rearranged");
            Assert.That(PositionsOf(worker), Is.EqualTo(arrangedAt));
        }

        [Test]
        public void SizesACallersBranchNodeFromTheBranchItRuns()
        {
            BehaviorTreeLayout.Arrange(worker);

            foreach (var caller in worker.graph.Nodes.OfType<RunBehaviorTreeGraphNode>())
            {
                var branch = caller.BehaviorTreeGraphAsset;
                var drawnAs = SubTreeFootprint.BoxAt(caller.Position.position, SubTreeFootprint.BoundsOf(branch.graph));

                Assert.That(caller.Position.size, Is.EqualTo(drawnAs.size), caller.NodeName + " is not its branch's size");
            }
        }

        [Test]
        public void ArrangesABranchBeforeTheTreeThatRunsIt()
        {
            var arranged = BehaviorTreeLayout.Arrange(worker).Select(tree => tree.tree).ToList();

            Assert.That(arranged.Last(), Is.EqualTo(PathOf("Worker")));
            Assert.That(arranged.IndexOf(PathOf("Engage")), Is.LessThan(arranged.IndexOf(PathOf("Worker"))));
            Assert.That(arranged.IndexOf(PathOf("SwordAttack")), Is.LessThan(arranged.IndexOf(PathOf("Engage"))));
        }

        [Test]
        public void LaysSiblingsOutLeftToRightInPriorityOrder()
        {
            BehaviorTreeLayout.Arrange(worker);

            foreach (var tree in trees)
            {
                foreach (var parent in tree.graph.Nodes)
                {
                    var children = tree.graph.ChildTransitionsInPriorityOrder(parent)
                        .Select(transition => transition.destination as BehaviorTreeNode)
                        .Where(child => child != null)
                        .ToList();

                    for (var next = 1; next < children.Count; next++)
                    {
                        Assert.That(children[next].Position.xMin, Is.GreaterThan(children[next - 1].Position.xMin),
                            tree.name + ": " + parent.NodeName + "'s children are out of priority order");
                    }
                }
            }
        }

        private static void PointBranchesAtTheCopies(IReadOnlyDictionary<string, BehaviorTreeGraphAsset> copiedFrom)
        {
            foreach (var copy in copiedFrom.Values)
            {
                foreach (var caller in copy.graph.Nodes.OfType<RunBehaviorTreeGraphNode>())
                {
                    var branch = caller.BehaviorTreeGraphAsset;
                    if (branch == null) continue;

                    if (copiedFrom.TryGetValue(AssetDatabase.GetAssetPath(branch), out var itsCopy))
                    {
                        caller.SetBehaviorTreeGraphAsset(itsCopy);
                    }
                }

                EditorUtility.SetDirty(copy);
            }

            AssetDatabase.SaveAssets();
        }

        private static string PathOf(string tree) => UnderTest + "/" + tree + ".asset";

        private static List<Rect> PositionsOf(BehaviorTreeGraphAsset tree) =>
            tree.graph.Nodes.Select(node => node.Position).ToList();
    }
}

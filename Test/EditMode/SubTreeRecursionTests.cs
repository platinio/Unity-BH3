using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    public class SubTreeRecursionTests
    {
        private const string Folder = "Assets/__SubTreeRecursionTests";

        [SetUp]
        public void CreateTheFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__SubTreeRecursionTests");
            NodeProblemCache.Invalidate();
        }

        [TearDown]
        public void DeleteTheFolder()
        {
            AssetDatabase.DeleteAsset(Folder);
            NodeProblemCache.Invalidate();
        }

        [Test]
        public void ANodeThatRunsItsOwnTreeFindsTheLoop()
        {
            var tree = Tree("A");
            var runsItself = Runs(tree, tree);

            Assert.That(NamesOf(runsItself), Is.EqualTo(new[] { "A", "A" }));
        }

        [Test]
        public void TwoTreesRunningEachOtherFindTheLoopFromEitherSide()
        {
            var a = Tree("A");
            var b = Tree("B");
            var aRunsB = Runs(a, b);
            var bRunsA = Runs(b, a);

            Assert.That(NamesOf(aRunsB), Is.EqualTo(new[] { "B", "A", "B" }));
            Assert.That(NamesOf(bRunsA), Is.EqualTo(new[] { "A", "B", "A" }));
        }

        [Test]
        public void ANodeAboveALoopFindsItInTheOrderItRuns()
        {
            var root = Tree("Root");
            var b = Tree("B");
            var c = Tree("C");
            var rootRunsB = Runs(root, b);
            Runs(b, c);
            Runs(c, c);

            Assert.That(NamesOf(rootRunsB), Is.EqualTo(new[] { "B", "C", "C" }));
        }

        [Test]
        public void TheSameBranchRunTwiceIsNotALoop()
        {
            var root = Tree("Root");
            var middle = Tree("Middle");
            var leaf = Tree("Leaf");
            var rootRunsMiddle = Runs(root, middle);
            var rootRunsLeaf = Runs(root, leaf);
            Runs(middle, leaf);
            Runs(middle, leaf);

            Assert.That(rootRunsMiddle.FindRecursion(), Is.Empty);
            Assert.That(rootRunsLeaf.FindRecursion(), Is.Empty);
            Assert.That(NodeProblemCache.For(rootRunsMiddle).Select(problem => problem.Summary),
                Has.None.Contains("recursion"));
        }

        [Test]
        public void ALoopIsAnErrorOnTheNodeNamingTheTreeToOpen()
        {
            var root = Tree("Root");
            var b = Tree("B");
            var c = Tree("C");
            var rootRunsB = Runs(root, b);
            Runs(b, c);
            Runs(c, b);

            var problem = NodeProblemCache.For(rootRunsB).Single(found => found.Summary.Contains("recursion"));

            Assert.That(problem.Severity, Is.EqualTo(NodeProblemSeverity.Error));
            Assert.That(problem.Summary, Does.Contain("B -> C -> B"));
            Assert.That(problem.Fix, Does.Contain("Open C"));
            Assert.That(problem.Repair, Is.Null, "breaking a loop means choosing a call site; nothing can pick one");
        }

        [Test]
        public void StartingATreeWithALoopThrowsNamingTheLoopInTheOrderItRuns()
        {
            var root = Tree("Root");
            var b = Tree("B");
            var c = Tree("C");
            Runs(root, b);
            Runs(b, c);
            Runs(c, c);

            Assert.That(() => root.graph.ThrowIfCausesRecursion(),
                Throws.Exception.With.Message.Contains("recursion: B -> C -> C"));
        }

        [Test]
        public void VerifyReportsALoopWithItsPath()
        {
            var a = Tree("A");
            var b = Tree("B");
            Runs(a, b);
            Runs(b, a);
            BehaviorTreeAuthoring.Save(a);
            BehaviorTreeAuthoring.Save(b);

            var findings = BehaviorTreeVerification.Verify(AssetDatabase.GetAssetPath(a));

            Assert.That(findings, Has.Some.EqualTo("A: sub-tree recursion — B -> A -> B"));
        }

        [Test]
        public void VerifyReportsASubTreeNodeWithNoTreeAssigned()
        {
            var tree = Tree("Unassigned");
            var runsNothing = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(tree, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(tree, tree.graph.EntryNode, runsNothing);
            BehaviorTreeAuthoring.Save(tree);

            var findings = BehaviorTreeVerification.Verify(AssetDatabase.GetAssetPath(tree));

            Assert.That(findings, Has.Some.Contains("sub-tree — (none assigned)"));
        }

        private static BehaviorTreeGraphAsset Tree(string name) => BehaviorTreeAuthoring.CreateTree($"{Folder}/{name}.asset");

        private static RunBehaviorTreeGraphNode Runs(BehaviorTreeGraphAsset caller, BehaviorTreeGraphAsset branch)
        {
            var node = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(caller, caller.graph.EntryNode, node);

            return node;
        }

        private static string[] NamesOf(RunBehaviorTreeGraphNode node) =>
            node.FindRecursion().Select(tree => tree.name).ToArray();
    }
}

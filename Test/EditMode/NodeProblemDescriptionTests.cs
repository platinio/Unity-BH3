using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The badge's hover text, which is a pure function of the problem list and therefore belongs in the
    /// cache that already owns the list.
    ///
    /// <para>
    /// It used to be built in the widget: a <c>StringBuilder</c> and a string for every problem-carrying node
    /// on every repaint, for text nobody sees unless they hover. Moving it here makes it cost one dictionary
    /// lookup per repaint instead — but only if it goes stale on exactly the same signal as the list, which
    /// is what these pin. A description that outlived the problems it describes would be a tooltip
    /// contradicting the badge beside it.
    /// </para>
    /// </summary>
    public class NodeProblemDescriptionTests
    {
        private const string Folder = "Assets/__NodeProblemDescriptionTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__NodeProblemDescriptionTests");

            FunctionEvaluator.InvalidateAll();
            NodeProblemCache.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
            NodeProblemCache.Invalidate();
        }

        private BarePortNode UnfedMove(string assetName)
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/{assetName}.asset");

            // BarePortNode.Position declares no default and is the node's only port, so an untouched node
            // reports exactly one problem.
            return BehaviorTreeAuthoring.AddNode<BarePortNode>(tree, 0.0f, 0.0f);
        }

        [Test]
        public void AHealthyNode_HasNoDescription()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Healthy.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.SetValue(tree, wait.Time, 1.0f, -200.0f, 0.0f);
            NodeProblemCache.Invalidate();

            Assert.That(NodeProblemCache.DescriptionOf(wait), Is.Empty,
                "an empty tooltip is what stops a hover box appearing on a node with nothing wrong");
        }

        [Test]
        public void ADescription_NamesTheProblem()
        {
            Assert.That(NodeProblemCache.DescriptionOf(UnfedMove("Named")), Does.Contain("Position"));
        }

        [Test]
        public void SeveralProblems_GetALineEach()
        {
            var move = UnfedMove("Several");

            System.Func<BehaviorTreeNode, IEnumerable<NodeProblem>> provider = candidate =>
                candidate is BarePortNode
                    ? new[] { new NodeProblem(NodeProblemSeverity.Warning, "second problem") }
                    : Enumerable.Empty<NodeProblem>();

            NodeProblemCache.AddProvider(provider);

            try
            {
                var description = NodeProblemCache.DescriptionOf(move);

                Assert.That(description, Does.Contain("Position").And.Contain("second problem"));
                Assert.That(description.Split('\n').Length, Is.EqualTo(NodeProblemCache.For(move).Count),
                    "one problem per line, or a two-problem node reads as one run-on sentence");
            }
            finally
            {
                // Providers are static and outlive the test.
                NodeProblemCache.RemoveProvider(provider);
            }
        }

        [Test]
        public void ReadingTwice_DoesNotRebuildTheText()
        {
            var move = UnfedMove("Cached");

            Assert.That(NodeProblemCache.DescriptionOf(move), Is.SameAs(NodeProblemCache.DescriptionOf(move)),
                "the same instance is the only observable difference between caching and rebuilding, and " +
                "rebuilding is what this text was moved out of the repaint path to stop");
        }

        [Test]
        public void AfterInvalidation_TheDescriptionIsRecomputed()
        {
            var move = UnfedMove("Stale");

            Assert.That(NodeProblemCache.DescriptionOf(move), Does.Not.Contain("added later"));

            System.Func<BehaviorTreeNode, IEnumerable<NodeProblem>> provider = candidate =>
                candidate is BarePortNode
                    ? new[] { new NodeProblem(NodeProblemSeverity.Error, "added later") }
                    : Enumerable.Empty<NodeProblem>();

            // AddProvider invalidates, which is the signal the description has to answer to as well.
            NodeProblemCache.AddProvider(provider);

            try
            {
                Assert.That(NodeProblemCache.DescriptionOf(move), Does.Contain("added later"),
                    "a tooltip that survived the invalidation would contradict the badge drawn beside it");
            }
            finally
            {
                NodeProblemCache.RemoveProvider(provider);
            }
        }

        [Test]
        public void ANodeWithNoGraph_IsNotCached()
        {
            // A bare node has never run Definition(), so it has no ports and reports nothing about itself.
            // The problem has to come from a provider, or there is no description to cache either way.
            var orphan = new WaitTime();

            System.Func<BehaviorTreeNode, IEnumerable<NodeProblem>> provider = candidate =>
                candidate is WaitTime
                    ? new[] { new NodeProblem(NodeProblemSeverity.Warning, "reported without a graph") }
                    : Enumerable.Empty<NodeProblem>();

            NodeProblemCache.AddProvider(provider);

            try
            {
                Assert.That(NodeProblemCache.DescriptionOf(orphan), Does.Contain("reported without a graph"),
                    "the node still gets an answer; it just does not get a stored one");

                Assert.That(NodeProblemCache.DescriptionOf(orphan),
                    Is.Not.SameAs(NodeProblemCache.DescriptionOf(orphan)),
                    "nothing can tell this class that a graphless node changed, so an entry for one could " +
                    "never be dropped -- the same rule For() already applies to the list");
            }
            finally
            {
                NodeProblemCache.RemoveProvider(provider);
            }
        }
    }
}

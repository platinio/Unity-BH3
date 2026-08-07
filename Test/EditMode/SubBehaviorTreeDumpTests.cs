using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A behavior tree can run another behavior tree, which can run another, to any depth. These cover that
    /// the dump follows the whole chain and, more importantly, that a tree which reaches itself is reported
    /// rather than hung on.
    ///
    /// These build real <see cref="BehaviorTreeGraphAsset"/> instances, so unlike the other dump fixtures
    /// they need a live Unity to run.
    /// </summary>
    [TestFixture]
    public class SubBehaviorTreeDumpTests
    {
        private BehaviorTreeGraphAsset[] created;

        [SetUp]
        public void SetUp() => created = new BehaviorTreeGraphAsset[0];

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
        }

        private BehaviorTreeGraphAsset NewTree(string name)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            asset.name = name;

            created = created.Concat(new[] { asset }).ToArray();

            return asset;
        }

        /// <summary>Parents a Run Behavior Tree Graph node under the entry of <paramref name="host"/>.</summary>
        private static RunBehaviorTreeGraphNode RunsSubTree(BehaviorTreeGraphAsset host, BehaviorTreeGraphAsset subTree, float x = 0.0f)
        {
            var runNode = new RunBehaviorTreeGraphNode { Position = new Rect(x, 100.0f, 150.0f, 100.0f) };
            host.graph.Nodes.Add(runNode);
            runNode.SetBehaviorTreeGraphAsset(subTree);

            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(host.graph.EntryNode, runNode, 0);
            host.graph.Transitions.Add(transition);

            return runNode;
        }

        [Test]
        public void ASubTreeIsExpandedInPlace()
        {
            var child = NewTree("Child");
            child.graph.Nodes.Add(new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) });

            var parent = NewTree("Parent");
            RunsSubTree(parent, child);

            string json = BehaviorTreeDump.ToJson(parent);

            StringAssert.Contains("\"subTree\"", json);
            StringAssert.Contains("\"asset\": \"Child\"", json,
                "The nested tree has to be named, or a dump cannot tell you which asset ran.");
        }

        [Test]
        public void NestingFollowsToAnyDepth()
        {
            var third = NewTree("Third");
            third.graph.Nodes.Add(new Selector { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) });

            var second = NewTree("Second");
            RunsSubTree(second, third);

            var first = NewTree("First");
            RunsSubTree(first, second);

            string json = BehaviorTreeDump.ToJson(first);

            Assert.Less(json.IndexOf("\"Second\""), json.IndexOf("\"Third\""),
                "Depth should read outermost to innermost.");
            StringAssert.Contains("\"asset\": \"Third\"", json, "Three levels of nesting must all be expanded.");
        }

        [Test]
        public void ATreeThatRunsItselfIsReportedNotFollowed()
        {
            var tree = NewTree("SelfRunner");
            RunsSubTree(tree, tree);

            string json = BehaviorTreeDump.ToJson(tree);

            StringAssert.Contains("recursion", json,
                "Self reference must be named as recursion rather than expanded forever.");
        }

        [Test]
        public void MutualRecursionIsReported()
        {
            var a = NewTree("A");
            var b = NewTree("B");

            RunsSubTree(a, b);
            RunsSubTree(b, a);

            string json = BehaviorTreeDump.ToJson(a);

            StringAssert.Contains("recursion", json);
            StringAssert.Contains("A -> B -> A", json,
                "The reported path is what makes an indirect cycle diagnosable.");
        }

        [Test]
        public void TheSameSubTreeUsedTwiceIsNotMistakenForRecursion()
        {
            var shared = NewTree("Shared");
            shared.graph.Nodes.Add(new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) });

            var parent = NewTree("Parent");
            RunsSubTree(parent, shared, -200.0f);
            RunsSubTree(parent, shared, 200.0f);

            string json = BehaviorTreeDump.ToJson(parent);

            Assert.IsFalse(json.Contains("recursion"),
                "Reusing a shared branch on two sides of a tree is the whole point of sub-trees, not a cycle.");
        }

        [Test]
        public void AnUnassignedSubTreeIsReportedRatherThanThrowing()
        {
            var parent = NewTree("Parent");
            RunsSubTree(parent, null);

            StringAssert.Contains("(none assigned)", BehaviorTreeDump.ToJson(parent),
                "A Run Behavior Tree Graph node with nothing assigned is a normal authoring state.");
        }
    }
}

using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A call site that is handed its tree through the <c>Tree</c> port instead of being given one in the
    /// inspector, which is how a weapon, a vehicle or an ability can carry the branch that runs it.
    ///
    /// <para>
    /// The hard part is not choosing the asset, it is that a clone built after the machine started has
    /// never been given the machine, a scope or the recorder and has never been awoken — startup did all of
    /// that once, for the tree that was picked in the inspector, and never comes back.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SubTreeChosenAtRuntimeTests
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

        private BehaviorTreeGraphAsset BranchThatKeepsRunning(string name)
        {
            var branch = NewTree(name);
            var leaf = BehaviorTreeAuthoring.AddNode<AlwaysRunningNode>(branch, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(branch, branch.graph.EntryNode, leaf);

            return branch;
        }

        private BehaviorTreeGraphAsset HostRunning(BehaviorTreeGraphAsset picked, out RunBehaviorTreeGraphNode runNode)
        {
            var host = NewTree("Host");

            runNode = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(host, 0.0f, 100.0f);
            runNode.SetBehaviorTreeGraphAsset(picked);
            BehaviorTreeAuthoring.Connect(host, host.graph.EntryNode, runNode);

            return host;
        }

        private static ValueSource<BehaviorTreeGraphAsset> Hand(
            BehaviorTreeGraphAsset host, RunBehaviorTreeGraphNode runNode, BehaviorTreeGraphAsset tree)
        {
            var source = BehaviorTreeAuthoring.AddNode<ValueSource<BehaviorTreeGraphAsset>>(host, 300.0f, 100.0f);
            source.Published = tree;
            source.Value.ValidlyConnectTo(runNode.Tree);

            return source;
        }

        private static void Run(BehaviorTreeGraphAsset host)
        {
            host.graph.OnAwake();
            host.graph.OnEnter();
            host.graph.OnUpdate();
        }

        private static AlwaysRunningNode LeafInside(RunBehaviorTreeGraphNode runNode) =>
            runNode.BehaviorTreeGraphInstance.Nodes.OfType<AlwaysRunningNode>().Single();

        private static void Declare(BehaviorTreeGraphAsset tree, string name, object value) =>
            tree.requiredDeclarations.Set(name, value);

        [Test]
        public void WithNothingOnThePortItRunsTheTreeItWasGivenInTheInspector()
        {
            var picked = BranchThatKeepsRunning("Picked");
            var host = HostRunning(picked, out var runNode);

            Run(host);

            Assert.AreEqual(picked, runNode.RunningTree);
        }

        [Test]
        public void ItRunsTheTreeThePortHandsIt()
        {
            var picked = BranchThatKeepsRunning("Picked");
            var handed = BranchThatKeepsRunning("Handed");
            var host = HostRunning(picked, out var runNode);
            Hand(host, runNode, handed);

            Run(host);

            Assert.AreEqual(handed, runNode.RunningTree, "the port's answer should have won over the inspector's");
        }

        [Test]
        public void TheTreeThePortHandsItIsActuallyTicked()
        {
            var picked = BranchThatKeepsRunning("Picked");
            var handed = BranchThatKeepsRunning("Handed");
            var host = HostRunning(picked, out var runNode);
            Hand(host, runNode, handed);

            Run(host);

            var leaf = LeafInside(runNode);
            Assert.AreEqual(1, leaf.EnterCalls, "a clone nobody awoke and entered runs nothing");
            Assert.IsTrue(leaf.IsRunning);
        }

        [Test]
        public void TheTreeThePortHandsItGetsAScopeOfItsOwn()
        {
            var picked = BranchThatKeepsRunning("Picked");
            var handed = BranchThatKeepsRunning("Handed");
            var host = HostRunning(picked, out var runNode);
            Hand(host, runNode, handed);

            Run(host);

            Assert.IsNotNull(LeafInside(runNode).VariableScope,
                "without a scope the branch resolves its own parameters against nothing");
        }

        [Test]
        public void ATreeReplacedWhileItIsStillRunningIsExitedFirst()
        {
            var runNode = AfterTheTreeIsReplaced(out var leafLeftBehind, out _);

            Assert.AreEqual(1, leafLeftBehind.ExitCalls, "anything the outgoing branch acquired on enter leaks otherwise");
            Assert.IsFalse(leafLeftBehind.IsRunning);
            Assert.IsNotNull(runNode);
        }

        [Test]
        public void TheCloneOfTheTreeItLeavesIsFreed()
        {
            AfterTheTreeIsReplaced(out _, out var cloneLeftBehind);

            Assert.IsTrue(cloneLeftBehind == null,
                "one leaked clone per swap is invisible until a unit has changed weapon a few hundred times");
        }

        [Test]
        public void HandedTheTreeItIsAlreadyRunningItKeepsTheOneItHas()
        {
            var picked = BranchThatKeepsRunning("Picked");
            var host = HostRunning(picked, out var runNode);
            Hand(host, runNode, picked);

            Run(host);
            var clone = runNode.BehaviorTreeGraphAssetInstance;
            runNode.OnEnter();

            Assert.AreEqual(clone, runNode.BehaviorTreeGraphAssetInstance,
                "rebuilding the branch it is already running would throw away everything it had counted");
        }

        [Test]
        public void ATreeThatDoesNotDeclareTheParametersIsRefusedAndSaysWhich()
        {
            var picked = BranchThatKeepsRunning("Picked");
            Declare(picked, "keepDistance", 0.0f);
            var handed = BranchThatKeepsRunning("Handed");

            var host = HostRunning(picked, out var runNode);
            runNode.RefreshParameters();
            Hand(host, runNode, handed);

            Assert.That(() => Run(host),
                Throws.Exception.With.Message.Contains("keepDistance").And.Message.Contains("Handed"));
        }

        [Test]
        public void ATreeThatDeclaresTheSameParametersIsAccepted()
        {
            var picked = BranchThatKeepsRunning("Picked");
            Declare(picked, "keepDistance", 0.0f);
            var handed = BranchThatKeepsRunning("Handed");
            Declare(handed, "keepDistance", 0.0f);

            var host = HostRunning(picked, out var runNode);
            runNode.RefreshParameters();
            Hand(host, runNode, handed);
            FeedTheDistance(host, runNode);

            Run(host);

            Assert.AreEqual(handed, runNode.RunningTree);
        }

        [Test]
        public void ATreeThatWouldRunItselfIsRefused()
        {
            var picked = BranchThatKeepsRunning("Picked");
            var loops = NewTree("Loops");
            var inner = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(loops, 0.0f, 100.0f);
            inner.SetBehaviorTreeGraphAsset(loops);
            BehaviorTreeAuthoring.Connect(loops, loops.graph.EntryNode, inner);

            var host = HostRunning(picked, out var runNode);
            Hand(host, runNode, loops);

            Assert.That(() => Run(host), Throws.Exception.With.Message.Contains("recursion"),
                "the awake-time walk only ever saw the serialized assets, so this loop is new to it");
        }

        private static void FeedTheDistance(BehaviorTreeGraphAsset host, RunBehaviorTreeGraphNode runNode)
        {
            var distance = BehaviorTreeAuthoring.AddNode<ValueSource<float>>(host, 300.0f, 300.0f);
            distance.Published = 3.0f;
            distance.Value.ValidlyConnectTo(runNode.valueInputs.Single(port => port.key == "keepDistance"));
        }

        /// <summary>
        /// Runs the picked tree, then hands the node a different one and enters it again without exiting
        /// first, which is the case the node has to put the outgoing branch down itself.
        /// </summary>
        private RunBehaviorTreeGraphNode AfterTheTreeIsReplaced(
            out AlwaysRunningNode leafLeftBehind, out BehaviorTreeGraphAsset cloneLeftBehind)
        {
            var picked = BranchThatKeepsRunning("Picked");
            var handed = BranchThatKeepsRunning("Handed");
            var host = HostRunning(picked, out var runNode);
            var source = Hand(host, runNode, picked);

            Run(host);

            leafLeftBehind = LeafInside(runNode);
            cloneLeftBehind = runNode.BehaviorTreeGraphAssetInstance;

            source.Published = handed;
            runNode.OnEnter();

            return runNode;
        }
    }
}

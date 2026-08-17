using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Exiting a branch has to reach inside the sub-tree it runs.
    ///
    /// <para>
    /// <see cref="ContainerNode.OnExit"/> cascades to children correctly <em>within</em> one graph, but a
    /// <see cref="RunBehaviorTreeGraphNode"/> is a leaf as far as its own graph is concerned — the nodes it
    /// drives live in a separate instance it reaches by hand. It propagates seven lifecycle calls into that
    /// instance, and <c>OnExit</c> was missing from the list, so the cascade stopped dead at the sub-tree
    /// boundary.
    /// </para>
    ///
    /// <para>
    /// Normal completion hid it: the inner containers exit their own children on the way out. <b>Abort did
    /// not.</b> A guard returns Failure from the <c>OnUpdateInternal</c> walk <em>before</em>
    /// <c>base.OnUpdateInternal()</c>, so <see cref="RunBehaviorTreeGraphNode.OnUpdate"/> never ran that
    /// frame, the inner graph was never ticked, and every node inside was left with
    /// <see cref="GraphCore.BaseGraphNode{TGraph,TNode,TNodeTransition}.IsRunning"/> still true and its
    /// <c>OnExit</c> never called. Anything acquired on enter and released on exit leaked permanently.
    /// </para>
    ///
    /// <para>
    /// This was live before reactive guards and independent of them — any guard on a
    /// <see cref="RunBehaviorTreeGraphNode"/> flipping false hit it. Preemption only matters here because it
    /// turns a rare occurrence into a routine one, which is why the fix is pinned before the feature is
    /// built on top of it.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SubTreeTeardownTests
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

        private static void Connect(BehaviorTreeGraphAsset host, BehaviorTreeNode parent, BehaviorTreeNode child, int index = 0)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, index);
            host.graph.Transitions.Add(transition);
        }

        private static T Add<T>(BehaviorTreeGraphAsset host, float x = 0.0f, float y = 100.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, y, 150.0f, 100.0f) };
            host.graph.Nodes.Add(node);

            return node;
        }

        /// <summary>A branch whose only leaf never finishes, so it is still running whenever it is aborted.</summary>
        private BehaviorTreeGraphAsset LongRunningBranch()
        {
            var branch = NewTree("Branch");
            var leaf = Add<AlwaysRunningNode>(branch);
            Connect(branch, branch.graph.EntryNode, leaf);

            return branch;
        }

        /// <summary>
        /// Entry -> Selector -> Run Behavior Tree Graph(branch), with an optional guard on the run node.
        /// Awoken, entered, and ticked once, so the sub-tree is genuinely mid-run when the test acts.
        /// </summary>
        private BehaviorTreeGraphAsset RunningHost(out RunBehaviorTreeGraphNode runNode, out CountingReactiveGuard guard, bool guarded)
        {
            var host = NewTree("Host");
            var selector = Add<Selector>(host);

            runNode = Add<RunBehaviorTreeGraphNode>(host, 0.0f, 300.0f);
            runNode.SetBehaviorTreeGraphAsset(LongRunningBranch());

            Connect(host, host.graph.EntryNode, selector);
            Connect(host, selector, runNode);

            guard = null;

            if (guarded)
            {
                guard = Add<CountingReactiveGuard>(host, 200.0f, 300.0f);
                guard.UpdateOwner(runNode);
            }

            host.graph.OnAwake();
            host.graph.OnEnter();
            host.graph.OnUpdate();

            return host;
        }

        /// <summary>
        /// The node that actually runs is a clone: the sub-tree asset is instantiated per call site, so the
        /// authored leaf is never the one ticked. Every assertion has to read the instance.
        /// </summary>
        private static AlwaysRunningNode LeafInside(RunBehaviorTreeGraphNode runNode)
        {
            return runNode.BehaviorTreeGraphInstance.Nodes.OfType<AlwaysRunningNode>().Single();
        }

        [Test]
        public void ASubTreeIsGenuinelyRunningBeforeAnythingAbortsIt()
        {
            RunningHost(out var runNode, out _, guarded: true);

            var leaf = LeafInside(runNode);

            Assert.AreEqual(1, leaf.EnterCalls, "the branch's leaf should have been entered through the sub-tree boundary");
            Assert.IsTrue(leaf.IsRunning, "and should still be running, which is what makes the abort below meaningful");
        }

        /// <summary>The bug as reported: a guard kills the branch, and the nodes inside it are orphaned.</summary>
        [Test]
        public void AGuardAbortingASubTreeExitsEveryNodeInsideIt()
        {
            var host = RunningHost(out var runNode, out var guard, guarded: true);

            var leaf = LeafInside(runNode);

            guard.Result = false;
            host.graph.OnUpdate();

            Assert.AreEqual(1, leaf.ExitCalls,
                "OnExit has to cross the sub-tree boundary; without it anything acquired on enter leaks for good");
            Assert.IsFalse(leaf.IsRunning,
                "a node left marked running is also a node the recorder shows entering and never leaving");
        }

        /// <summary>
        /// The same cascade reached the ordinary way — a parent exiting — rather than through a guard.
        /// Stated separately because it is the path that <em>appeared</em> to work: it does, but only because
        /// the inner containers unwound themselves, so it never proved the boundary was crossed.
        /// </summary>
        [Test]
        public void ExitingAParentCascadesIntoASubTree()
        {
            var host = RunningHost(out var runNode, out _, guarded: false);

            var leaf = LeafInside(runNode);

            host.graph.EntryNode.OnNodeExit();

            Assert.AreEqual(1, leaf.ExitCalls, "the cascade must reach through the run node into its instance");
            Assert.IsFalse(leaf.IsRunning);
        }

        [Test]
        public void EveryNodeInsideAnAbortedSubTreeIsLeftStopped()
        {
            var host = RunningHost(out var runNode, out var guard, guarded: true);

            guard.Result = false;
            host.graph.OnUpdate();

            CollectionAssert.IsEmpty(
                runNode.BehaviorTreeGraphInstance.Nodes.Where(node => node.IsRunning).ToList(),
                "not just the leaf — no node in the branch may survive the abort still marked running");
        }

        /// <summary>
        /// Exiting a branch that never started must not clone the asset. <c>ContainerNode.OnExit</c> calls
        /// <c>OnNodeExit</c> on every child regardless of whether it ran, so without the
        /// <see cref="RunBehaviorTreeGraphNode.HasBehaviorTreeGraphInstance"/> check a selector exiting would
        /// instantiate every branch it declined to enter.
        /// </summary>
        [Test]
        public void ExitingASubTreeThatNeverRanDoesNotInstantiateIt()
        {
            var host = NewTree("Host");
            var runNode = Add<RunBehaviorTreeGraphNode>(host);
            runNode.SetBehaviorTreeGraphAsset(LongRunningBranch());

            runNode.OnNodeExit();

            Assert.IsFalse(runNode.HasBehaviorTreeGraphInstance,
                "a branch nobody entered should cost nothing to exit");
        }

        #region Releasing the instance

        /// <summary>
        /// The clone a call site makes is freed when the tree holding it is destroyed.
        ///
        /// <para>
        /// Nothing else can free it: the instance is created per call site per agent, the machine destroys
        /// only the root instance it made itself, and <see cref="BehaviorTreeGraph.OnDestroy"/> cascades node
        /// destruction without knowing what any node owns. Until <c>RunBehaviorTreeGraphNode</c> overrode
        /// <c>OnDestroy</c>, every sub-tree clone simply outlived its agent for the session.
        /// </para>
        /// </summary>
        [Test]
        public void DestroyingAHostReleasesTheSubTreeInstanceItsCallSiteMade()
        {
            var host = RunningHost(out var runNode, out _, guarded: false);

            var instance = runNode.BehaviorTreeGraphAssetInstance;

            Assert.IsFalse(instance == null, "the call site instantiated its branch in order to run it");

            host.graph.OnDestroy();

            // Unity's overloaded equality: a destroyed object compares equal to null while the managed
            // reference this test still holds keeps it reachable, so this is the only way to ask.
            Assert.IsTrue(instance == null,
                "the branch's clone has to go with the tree that owned it — one leaked ScriptableObject per "
                + "call site per agent is invisible until a wave-based scene has spawned a few hundred");

            Assert.IsFalse(runNode.HasBehaviorTreeGraphInstance,
                "and the call site stops claiming to hold one");
        }

        /// <summary>
        /// Destroying a branch that never ran must not clone it — the same trap
        /// <see cref="ExitingASubTreeThatNeverRanDoesNotInstantiateIt"/> pins for exit, and a worse one here.
        ///
        /// <para>
        /// <c>BehaviorTreeGraph.OnDestroy</c> calls <c>OnDestroy</c> on <em>every</em> node, so a tree with ten
        /// branches the agent never took would, if this read the lazy property instead of the field,
        /// instantiate all ten at teardown purely in order to destroy them. A leak fix that allocates.
        /// </para>
        /// </summary>
        [Test]
        public void DestroyingASubTreeThatNeverRanDoesNotInstantiateIt()
        {
            var host = NewTree("Host");
            var runNode = Add<RunBehaviorTreeGraphNode>(host);
            runNode.SetBehaviorTreeGraphAsset(LongRunningBranch());

            runNode.OnDestroy();

            Assert.IsFalse(runNode.HasBehaviorTreeGraphInstance,
                "a branch nobody entered should cost nothing to destroy either");
        }

        /// <summary>
        /// The release reaches all the way down, not one level.
        ///
        /// <para>
        /// A sub-tree can itself call a sub-tree, and each level clones again — so the leak multiplied with
        /// exactly the nested, modular trees this tool encourages. The inner call site lives inside the
        /// <em>outer clone</em>, which means the only thing that can ever reach it is a walk that goes through
        /// that clone before destroying it. Destroying outermost-first would cut the path and leak everything
        /// below.
        /// </para>
        /// </summary>
        [Test]
        public void DestroyingAHostReleasesNestedSubTreeInstancesToo()
        {
            var innermost = LongRunningBranch();

            var middle = NewTree("Middle");
            var authoredInnerCall = Add<RunBehaviorTreeGraphNode>(middle);
            authoredInnerCall.SetBehaviorTreeGraphAsset(innermost);
            Connect(middle, middle.graph.EntryNode, authoredInnerCall);

            var host = NewTree("Host");
            var outerCall = Add<RunBehaviorTreeGraphNode>(host);
            outerCall.SetBehaviorTreeGraphAsset(middle);
            Connect(host, host.graph.EntryNode, outerCall);

            host.graph.OnAwake();

            var outerInstance = outerCall.BehaviorTreeGraphAssetInstance;

            // The call site that actually runs is the one inside the outer clone, not the authored node.
            var runningInnerCall = outerCall.BehaviorTreeGraphInstance.Nodes
                .OfType<RunBehaviorTreeGraphNode>().Single();

            var innerInstance = runningInnerCall.BehaviorTreeGraphAssetInstance;

            Assert.IsFalse(outerInstance == null, "both levels cloned on the way up");
            Assert.IsFalse(innerInstance == null);
            Assert.AreNotSame(outerInstance, innerInstance, "and they are genuinely two objects");

            host.graph.OnDestroy();

            Assert.IsTrue(outerInstance == null, "the outer clone is released");
            Assert.IsTrue(innerInstance == null,
                "and so is the one nested inside it — reachable only through the outer clone, so destroying "
                + "the outer one first would strand it permanently");
        }

        /// <summary>
        /// Destroying twice is harmless. Worth pinning because the second call runs against a field holding a
        /// destroyed object, where Unity's overloaded equality is the only thing standing between this and a
        /// re-instantiate through the lazy getter.
        /// </summary>
        [Test]
        public void DestroyingAHostTwiceIsHarmless()
        {
            var host = RunningHost(out var runNode, out _, guarded: false);

            host.graph.OnDestroy();

            Assert.DoesNotThrow(() => host.graph.OnDestroy(), "teardown has to tolerate being repeated");

            Assert.IsFalse(runNode.HasBehaviorTreeGraphInstance,
                "and must not have quietly built a fresh clone to destroy on the way through");
        }

        #endregion
    }
}

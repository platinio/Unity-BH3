using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A call site makes its own copy of the sub-tree, and only when it is told to.
    ///
    /// <para>
    /// The copy is what stops two agents running one branch from sharing node state, and it used to be made by
    /// the first read of the property that returned it. That turned every reader into a writer. The canvas
    /// preview asked whether a copy existed in order to decide what to draw, answered its own question by
    /// making one, and drew that copy from then on — so a sub-tree could be edited without the parent canvas
    /// ever changing, until a play-mode boundary cleared the field and the next repaint copied the asset again.
    /// </para>
    ///
    /// <para>
    /// The two halves are pinned separately: reading never creates, and the explicit verb creates once.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SubTreeInstanceCreationTests
    {
        private readonly List<BehaviorTreeGraphAsset> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }

            created.Clear();
        }

        private BehaviorTreeGraphAsset NewTree(string name)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            asset.name = name;

            created.Add(asset);

            return asset;
        }

        /// <summary>A call site pointed at a one-leaf branch, with nothing awoken or entered yet.</summary>
        private RunBehaviorTreeGraphNode CallSite()
        {
            var branch = NewTree("Branch");
            var leaf = BehaviorTreeAuthoring.AddNode<AlwaysRunningNode>(branch, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(branch, branch.graph.EntryNode, leaf);

            var host = NewTree("Host");
            var runNode = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(host, 0.0f, 100.0f);
            runNode.SetBehaviorTreeGraphAsset(branch);
            BehaviorTreeAuthoring.Connect(host, host.graph.EntryNode, runNode);

            return runNode;
        }

        [Test]
        public void ReadingTheSubTreeInstanceDoesNotCreateOne()
        {
            var runNode = CallSite();

            Assert.IsTrue(runNode.BehaviorTreeGraphAssetInstance == null,
                "a call site that has not run yet holds no copy of its branch");

            Assert.IsFalse(runNode.HasBehaviorTreeGraphInstance,
                "and asking whether it holds one must not be what makes it");
        }

        [Test]
        public void TheSubTreeIsInstantiatedOnceAndThenReused()
        {
            var runNode = CallSite();

            var instance = runNode.GetOrCreateBehaviorTreeGraphAssetInstance();
            created.Add(instance);

            Assert.IsFalse(instance == null, "the verb makes the copy");

            Assert.AreNotSame(runNode.BehaviorTreeGraphAsset, instance,
                "and it is a copy, so editing the asset cannot reach a running branch");

            Assert.AreSame(instance, runNode.GetOrCreateBehaviorTreeGraphAssetInstance(),
                "a second ask reuses it, so every tick of one call site drives the same nodes");
        }
    }
}

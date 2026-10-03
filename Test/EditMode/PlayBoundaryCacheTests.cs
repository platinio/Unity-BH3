using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// What the editor's static caches look like on the far side of a play-mode boundary when nothing
    /// reloaded the domain. <see cref="PlaySessionResetTests"/> pins the runtime statics; these pin the
    /// editor ones, which a domain reload used to empty for free.
    /// </summary>
    public class PlayBoundaryCacheTests
    {
        private const string Folder = "Assets/__PlayBoundaryCacheTests";

        private BehaviorTreeGraphAsset tree;

        private sealed class CountingIndex : GraphIndex<object>
        {
            public int Builds { get; private set; }

            protected override object Build(BehaviorTreeGraph graph)
            {
                Builds++;
                return new object();
            }

            public object Read(BehaviorTreeGraph graph) => For(graph);
        }

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__PlayBoundaryCacheTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
        }

        [TearDown]
        public void TearDown()
        {
            GraphIndex.InvalidateAll();
            NodeProblemCache.Invalidate();
            AssetDatabase.DeleteAsset(Folder);
        }

        [TestCase(PlayModeStateChange.EnteredPlayMode)]
        [TestCase(PlayModeStateChange.EnteredEditMode)]
        public void AGraphIndex_IsDroppedOnArrival(PlayModeStateChange arrival)
        {
            var index = new CountingIndex();

            index.Read(tree.graph);
            index.Read(tree.graph);
            Assert.AreEqual(1, index.Builds, "Fixture check: the second read is a hit.");

            GraphIndex.OnPlayModeStateChanged(arrival);
            index.Read(tree.graph);

            Assert.AreEqual(2, index.Builds,
                "An entry that survives the boundary is keyed on a scene graph the boundary just replaced.");
        }

        [TestCase(PlayModeStateChange.ExitingEditMode)]
        [TestCase(PlayModeStateChange.ExitingPlayMode)]
        public void AGraphIndex_IsKeptOnDeparture(PlayModeStateChange departure)
        {
            var index = new CountingIndex();

            index.Read(tree.graph);
            GraphIndex.OnPlayModeStateChanged(departure);
            index.Read(tree.graph);

            Assert.AreEqual(1, index.Builds,
                "The canvas still repaints on the way out; dropping here only buys a rebuild that the arrival drops again.");
        }

        [TestCase(PlayModeStateChange.EnteredPlayMode)]
        [TestCase(PlayModeStateChange.EnteredEditMode)]
        public void NodeProblems_AreAskedAgainOnArrival(PlayModeStateChange arrival)
        {
            var node = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var asked = 0;

            System.Func<BehaviorTreeNode, IEnumerable<NodeProblem>> provider = _ =>
            {
                asked++;
                return Enumerable.Empty<NodeProblem>();
            };

            NodeProblemCache.AddProvider(provider);

            try
            {
                NodeProblemCache.For(node);
                NodeProblemCache.For(node);
                Assert.AreEqual(1, asked, "Fixture check: the second read is a hit.");

                NodeProblemCache.OnPlayModeStateChanged(arrival);
                NodeProblemCache.For(node);

                Assert.AreEqual(2, asked);
            }
            finally
            {
                NodeProblemCache.RemoveProvider(provider);
            }
        }

        [Test]
        public void TheConnectionTexture_IsRebuiltAfterUnityDestroysIt()
        {
            const float width = 7.25f;

            var first = GraphGUI.AliasedBezierTexture(width);
            Assert.AreSame(first, GraphGUI.AliasedBezierTexture(width), "Fixture check: the texture is cached.");

            // What entering play mode without a domain reload does to an editor-created texture.
            Object.DestroyImmediate(first);

            var second = GraphGUI.AliasedBezierTexture(width);

            Assert.IsTrue(second != null,
                "A destroyed texture handed back from the cache draws every script-graph connection with nothing.");
        }

        [Test]
        public void TheConnectionTexture_IsNotUnloadedWithTheScene()
        {
            Assert.AreEqual(HideFlags.HideAndDontSave, GraphGUI.AliasedBezierTexture(9.5f).hideFlags);
        }
    }
}

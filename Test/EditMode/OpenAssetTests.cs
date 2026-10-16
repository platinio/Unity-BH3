using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    public class OpenAssetTests
    {
        private const string Folder = "Assets/__OpenAssetTests";

        private BehaviorTreeGraphAsset tree;
        private Dictionary<BehaviorTreeGraphWindow, GraphCore.GraphReference> treeWindowsOpenBefore;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__OpenAssetTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            treeWindowsOpenBefore = RememberOpenTreeWindows();
        }

        [TearDown]
        public void TearDown()
        {
            RestoreTreeWindows();

            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void OpeningATree_OpensItInTheTreeWindow()
        {
            Assert.That(BehaviorTreeCanvas.OnOpenAsset(IdOf(tree), line: 0), Is.True);

            AssertTheActiveTreeWindowShows(tree);
        }

        [Test]
        public void OpeningATreeThroughUnity_ReachesTheTreeWindow()
        {
            AssetDatabase.OpenAsset(tree);

            AssertTheActiveTreeWindowShows(tree);
        }

        [Test]
        public void OpeningAGraphAssetThatIsNotATree_IsLeftToUnity()
        {
            var notATree = ScriptableObject.CreateInstance<NotATreeGraphAsset>();

            try
            {
                Assert.That(BehaviorTreeCanvas.OnOpenAsset(IdOf(notATree), line: 0), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(notATree);
            }
        }

        private static void AssertTheActiveTreeWindowShows(BehaviorTreeGraphAsset expectedTree)
        {
            var window = GraphCore.GraphWindow.active;

            Assert.That(window != null, Is.True, "no graph window is active");
            Assert.That(window.reference.rootObject, Is.SameAs(expectedTree));
        }

#if UNITY_6000_4_OR_NEWER
        private static EntityId IdOf(Object asset) => asset.GetEntityId();
#else
        private static int IdOf(Object asset) => asset.GetInstanceID();
#endif

        private static Dictionary<BehaviorTreeGraphWindow, GraphCore.GraphReference> RememberOpenTreeWindows()
        {
            var openWindows = new Dictionary<BehaviorTreeGraphWindow, GraphCore.GraphReference>();

            foreach (var window in Resources.FindObjectsOfTypeAll<BehaviorTreeGraphWindow>())
                openWindows[window] = window.reference;

            return openWindows;
        }

        private void RestoreTreeWindows()
        {
            if (treeWindowsOpenBefore.Count == 0)
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<BehaviorTreeGraphWindow>())
                    window.Close();

                return;
            }

            foreach (var windowAndReference in treeWindowsOpenBefore)
            {
                if (windowAndReference.Key != null)
                    windowAndReference.Key.reference = windowAndReference.Value;
            }
        }
    }
}

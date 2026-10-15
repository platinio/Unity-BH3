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
        private bool wasTreeWindowOpen;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__OpenAssetTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            wasTreeWindowOpen = EditorWindow.HasOpenInstances<BehaviorTreeGraphWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (!wasTreeWindowOpen)
                CloseTreeWindows();

            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void OpeningATree_OpensItInTheTreeWindow()
        {
            bool handled = BehaviorTreeCanvas.OnOpenAsset(IdOf(tree), 0);

            Assert.That(handled, Is.True);
            Assert.That(EditorWindow.HasOpenInstances<BehaviorTreeGraphWindow>(), Is.True);
        }

        [Test]
        public void OpeningAnotherAsset_IsLeftToUnity()
        {
            var notATree = new Texture2D(1, 1);

            try
            {
                Assert.That(BehaviorTreeCanvas.OnOpenAsset(IdOf(notATree), 0), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(notATree);
            }
        }

#if UNITY_6000_4_OR_NEWER
        private static EntityId IdOf(Object asset) => asset.GetEntityId();
#else
        private static int IdOf(Object asset) => asset.GetInstanceID();
#endif

        private static void CloseTreeWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<BehaviorTreeGraphWindow>())
                window.Close();
        }
    }
}

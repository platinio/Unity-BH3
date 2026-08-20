using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A transition's <see cref="PlaceHolderNode"/>s are hit targets for its three line segments — canvas
    /// scaffolding rebuilt whenever a window opens, with nothing authored about them.
    ///
    /// <para>
    /// They used to be serialized, so opening a tree added three nodes per transition to a file nobody had
    /// edited, and every walk over the element collection paid for them at runtime forever after. These pin
    /// that they stay out of the asset while remaining present in the live graph, which is what the canvas
    /// picks against.
    /// </para>
    /// </summary>
    public class EphemeralPlaceholderTests
    {
        private const string Folder = "Assets/__EphemeralPlaceholderTests";

        private const string TreePath = Folder + "/Tree.asset";

        private BehaviorTreeGraphAsset tree;

        private PlaceHolderNode placeholder;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__EphemeralPlaceholderTests");

            tree = BehaviorTreeAuthoring.CreateTree(TreePath);

            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 100.0f);
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 300.0f);
            BehaviorTreeAuthoring.Connect(tree, tree.graph.EntryNode, sequence);
            BehaviorTreeAuthoring.Connect(tree, sequence, wait);

            // What the transition widget does when a canvas opens.
            placeholder = new PlaceHolderNode(tree.graph.Transitions.First());
            tree.graph.elements.Add(placeholder);

            BehaviorTreeAuthoring.Save(tree);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        /// <summary>
        /// Reads the saved file back as a genuinely fresh deserialization.
        ///
        /// <para>
        /// Through a copy rather than by reimporting in place: <c>LoadAssetAtPath</c> hands back the instance
        /// already in memory, placeholders and all, so reimporting proves nothing about what was written. A
        /// copy is loaded from the bytes on disk — the same thing a fresh checkout or a player build sees.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset Reload()
        {
            var copyPath = Folder + "/Reloaded.asset";

            AssetDatabase.DeleteAsset(copyPath);
            AssetDatabase.CopyAsset(TreePath, copyPath);
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(copyPath);
        }

        [Test]
        public void APlaceHolderNodeIsNotWrittenToTheAsset()
        {
            var onDisk = System.IO.File.ReadAllText(TreePath);

            Assert.That(onDisk, Does.Not.Contain("ArcaneOnyx.BehaviorTree.PlaceHolderNode"),
                "canvas scaffolding in the file is read back as though somebody had authored it");
        }

        [Test]
        public void TheGraphStillHoldsItsPlaceHolderNodeInMemory()
        {
            Assert.That(tree.graph.elements.Contains(placeholder), Is.True,
                "the canvas picks transition lines through these, so keeping them out of the asset must not "
                + "mean taking them out of the open graph");
        }

        [Test]
        public void APlaceHolderNodeDoesNotSurviveAReload()
        {
            var reloaded = Reload();

            Assert.That(reloaded.graph.elements.OfType<PlaceHolderNode>(), Is.Empty,
                "a tree loaded outside a canvas -- at runtime, or by the CLI tooling -- has no reason to "
                + "carry hit targets for lines nobody is drawing");
        }

        [Test]
        public void EverythingActuallyAuthoredSurvivesTheRoundTrip()
        {
            var reloaded = Reload();

            Assert.That(reloaded.graph.Transitions.Count, Is.EqualTo(2),
                "both transitions come back");
            Assert.That(reloaded.graph.Nodes.OfType<Sequence>().Count(), Is.EqualTo(1));
            Assert.That(reloaded.graph.Nodes.OfType<WaitTime>().Count(), Is.EqualTo(1));
            Assert.That(reloaded.graph.EntryNode, Is.Not.Null,
                "filtering one element type must not disturb the rest of the graph");
        }
    }
}

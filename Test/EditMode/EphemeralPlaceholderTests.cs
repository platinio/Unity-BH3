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

        // ------------------------------------------------------------------ the assets already on disk

        /// <summary>
        /// A real tree written by the version that persisted its placeholders, frozen as a fixture.
        ///
        /// <para>
        /// The tests above build their fixture with the <em>current</em> code, so they say nothing about the
        /// case this change actually has to survive: a file whose placeholder objects are defined inside
        /// <c>PlaceHolderNodes</c> and referred to from the element list by <c>$id</c>. Getting that wrong
        /// does not lose a transition, it fails the entire asset in the serializer — which is what happened
        /// on the first attempt at this fix.
        /// </para>
        ///
        /// <para>
        /// A copy of a sample tree rather than the sample itself, and a copy taken at the time the fixture
        /// was added rather than a live reference: once anybody opens the original in a canvas and saves it,
        /// it stops being legacy data and a test pointed at it would quietly stop testing anything.
        /// </para>
        /// </summary>
        private const string LegacyFixture =
            "Assets/ArcaneOnyx/BH3/Test/EditMode/Fixtures/LegacyPlaceholders.asset";

        [Test]
        public void TheLegacyFixtureIsStillLegacy()
        {
            Assert.That(System.IO.File.Exists(LegacyFixture), Is.True, "the fixture has gone missing");

            Assert.That(System.IO.File.ReadAllText(LegacyFixture), Does.Contain("\"PlaceHolderNodes\":["),
                "the fixture has been re-saved by the current code, so the tests below no longer exercise "
                + "the migration they exist for -- restore it from a tree written before this change");
        }

        [Test]
        public void ALegacyAssetStillDeserializes()
        {
            var legacy = LoadLegacyCopy();

            Assert.That(legacy, Is.Not.Null, "the whole asset failed to load");
            Assert.That(legacy.graph, Is.Not.Null);
            Assert.That(legacy.graph.EntryNode, Is.Not.Null,
                "dropping the stored placeholders must not dangle the references the element list makes to "
                + "them -- when it does, the serializer gives up on the entire tree, not just the transition");
            Assert.That(legacy.graph.Transitions.Count, Is.EqualTo(4));
            Assert.That(legacy.graph.Nodes.Count, Is.GreaterThan(1));
        }

        [Test]
        public void ALegacyAssetShedsItsStoredPlaceholders()
        {
            Assert.That(LoadLegacyCopy().graph.elements.OfType<PlaceHolderNode>(), Is.Empty,
                "the placeholders stored in the file are stale by definition -- the canvas rebuilds its own");
        }

        private static BehaviorTreeGraphAsset LoadLegacyCopy()
        {
            var copyPath = Folder + "/LegacyCopy.asset";

            AssetDatabase.DeleteAsset(copyPath);
            AssetDatabase.CopyAsset(LegacyFixture, copyPath);
            AssetDatabase.Refresh();

            return AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(copyPath);
        }
    }
}

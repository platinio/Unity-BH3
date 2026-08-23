using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Covers the one thing in this project allowed to delete a graph: the save-time cleanup that replaced
    /// the project-wide ledger and the canvas's per-GUI-event sweep.
    /// <para>
    /// Every test here is about <em>what is still there afterwards</em>. A deleter is only as good as the
    /// things it refuses to take, so the cases that assert survival — a referenced graph, a standalone
    /// Function, an orphan whose node came back before the save — carry as much weight as the one that
    /// asserts removal.
    /// </para>
    /// </summary>
    public class OrphanedScriptGraphCleanupTests
    {
        private const string Folder = "Assets/__orphancleanup";
        private const string LegacyRepositoryFolder = "Assets/BehaviorTree.Generated";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__orphancleanup");
            FunctionEvaluator.InvalidateAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A saved tree with one node reading an embedded graph stored inside that tree.</summary>
        private static (BehaviorTreeGraphAsset tree, VisualScriptGraphVariable node, string path) TreeWithEmbeddedGraph(
            string treeName)
        {
            var path = $"{Folder}/{treeName}.asset";
            var tree = BehaviorTreeAuthoring.CreateTree(path);
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);

            node.SetScriptGraph(BehaviorTreeAuthoring.CreateVariableReadGraph(tree, "hp", false));

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            return (tree, node, path);
        }

        /// <summary>A minimal standalone Function, stored at its own path rather than inside a tree.</summary>
        private static FunctionGraphAsset StandaloneFunction(string assetName)
        {
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = function.graph;

            var input = new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) };
            var output = new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) };
            graph.units.Add(input);
            graph.units.Add(output);

            graph.controlInputDefinitions.Add(new Unity.VisualScripting.ControlInputDefinition
            {
                key = FunctionGraphAsset.EnterKey, label = FunctionGraphAsset.EnterKey
            });
            graph.controlOutputDefinitions.Add(new Unity.VisualScripting.ControlOutputDefinition
            {
                key = FunctionGraphAsset.ExitKey, label = FunctionGraphAsset.ExitKey
            });
            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(bool)
            });

            graph.PortDefinitionsChanged();
            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            return function;
        }

        /// <summary>How many script graphs are stored inside the asset at <paramref name="path"/>.</summary>
        private static int ScriptGraphsInside(string path)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .Count(representation => representation is Unity.VisualScripting.ScriptGraphAsset);
        }

        // ------------------------------------------------------------------ what the save removes

        /// <summary>
        /// Spec test 7. The whole point of moving deletion to save time: the graph goes when its owner does,
        /// and not a moment earlier.
        /// </summary>
        [Test]
        public void DeletingTheNodeThatOwnedAnEmbeddedGraph_RemovesTheSubAssetOnTheNextSave()
        {
            var (tree, node, path) = TreeWithEmbeddedGraph("OwnerDeleted");

            Assert.That(ScriptGraphsInside(path), Is.EqualTo(1),
                "precondition: the referenced graph must survive the save that stored it");

            tree.graph.elements.Remove(node);
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            Assert.That(ScriptGraphsInside(path), Is.EqualTo(0),
                "the graph nothing references any more must be gone from the tree");
        }

        // ------------------------------------------------------------------ what the save must never touch

        /// <summary>
        /// The other half of spec test 7, and the bug class this whole feature exists to make
        /// unrepresentable. The old deleter's candidate set was project-wide, so a graph shared between two
        /// trees could be destroyed out from under the other one. Candidates are now sub-assets of the tree
        /// being saved, which a standalone Function at its own path can never be.
        /// </summary>
        [Test]
        public void AStandaloneFunctionReferencedByTheTree_IsNeverTouchedBySave()
        {
            var functionPath = $"{Folder}/Shared.asset";
            var function = StandaloneFunction("Shared");

            var treePath = $"{Folder}/UsesShared.asset";
            var tree = BehaviorTreeAuthoring.CreateTree(treePath);
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            node.SetFunction(function);

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            // Orphan everything this tree could possibly own, then save again. The Function is not the
            // tree's to delete under any circumstances.
            tree.graph.elements.Remove(node);
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            Assert.That(AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(functionPath), Is.Not.Null,
                "a standalone Function must survive a save of a tree that stopped referencing it");
        }

        /// <summary>
        /// The undo case named by the step: an orphan that stops being one before anybody saves was never a
        /// candidate. This is what replaces undo support — deletion is not undoable, so it must not happen
        /// until the edit is committed.
        /// </summary>
        [Test]
        public void AGraphWhoseNodeComesBackBeforeTheSave_Survives()
        {
            var (tree, node, path) = TreeWithEmbeddedGraph("UndoneDeletion");

            tree.graph.elements.Remove(node);
            tree.graph.elements.Add(node);

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            Assert.That(ScriptGraphsInside(path), Is.EqualTo(1),
                "nothing may be destroyed for an edit that was reverted before the save");
        }

        /// <summary>Saving a healthy tree repeatedly must be a no-op, not a slow erosion of its contents.</summary>
        [Test]
        public void SavingATreeWithNoOrphans_DestroysNothing()
        {
            var (tree, _, path) = TreeWithEmbeddedGraph("Healthy");

            for (int i = 0; i < 3; i++)
            {
                EditorUtility.SetDirty(tree);
                AssetDatabase.SaveAssets();
            }

            Assert.That(ScriptGraphsInside(path), Is.EqualTo(1),
                "a referenced graph must survive any number of saves");
        }

        // ------------------------------------------------------------------ the finder's own refusals

        /// <summary>
        /// A tree the finder cannot read reports nothing rather than reporting everything. Read the other
        /// way round, an unreadable tree would look like "every sub-asset is unreferenced" and the cleanup
        /// would take the lot — the single worst thing this code could do.
        /// </summary>
        [Test]
        public void TheFinderRefusesToAnswerWithoutATreeAndAPath()
        {
            var (_, _, path) = TreeWithEmbeddedGraph("Refusals");

            Assert.That(OrphanedScriptGraphs.Of(null, path), Is.Empty, "no tree means no candidates");

            var tree = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
            Assert.That(OrphanedScriptGraphs.Of(tree, null), Is.Empty, "no path means no candidates");
            Assert.That(OrphanedScriptGraphs.Of(tree, string.Empty), Is.Empty, "no path means no candidates");
        }

        /// <summary>
        /// The lint and the cleanup must name the same set, which is why they share one finder. This is the
        /// finder answering the lint's remaining case: a graph still stored on disk that the tree no longer
        /// references. Reporting it is all that happens here — it is still there afterwards, because only a
        /// save may remove it.
        /// </summary>
        [Test]
        public void AnUnreferencedGraphStillOnDisk_IsNamedWithoutBeingDeleted()
        {
            var (tree, node, path) = TreeWithEmbeddedGraph("Reported");

            tree.graph.elements.Remove(node);

            var orphans = OrphanedScriptGraphs.Of(tree, path);

            Assert.That(orphans, Is.Not.Empty, "an unreferenced sub-asset must be found");
            Assert.That(orphans.All(orphan => orphan != null), Is.True, "a named orphan must still exist");
            Assert.That(ScriptGraphsInside(path), Is.EqualTo(1),
                "finding an orphan must not remove it — only a save may do that");
        }

        // ------------------------------------------------------------------ the repository is gone

        /// <summary>
        /// Spec test 8 and acceptance criterion 3. The ledger dirtied one project-wide file on every tree
        /// edit, which is what made two people editing unrelated trees collide in version control. The
        /// check that matters is not that the file was deleted once, but that a full authoring round trip
        /// no longer creates one.
        /// </summary>
        [Test]
        public void AuthoringAnEmbeddedGraph_CreatesNoProjectWideLedger()
        {
            TreeWithEmbeddedGraph("NoLedger");

            Assert.That(AssetDatabase.IsValidFolder(LegacyRepositoryFolder), Is.False,
                "authoring a tree must not recreate the generated-ledger folder");
            Assert.That(
                AssetDatabase.LoadMainAssetAtPath($"{LegacyRepositoryFolder}/ScriptGraphAssetsRepository.asset"),
                Is.Null,
                "nothing may recreate the script graph repository asset");
        }
    }
}

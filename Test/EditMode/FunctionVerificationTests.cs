using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Covers what <c>bt_verify</c> reports about Functions, and the extract command that promotes an
    /// embedded one-off into a shared asset.
    /// <para>
    /// Every lint here replaces something that previously failed at runtime or not at all, so each test's
    /// real subject is "does this get <em>named</em>", not merely "does it get noticed".
    /// </para>
    /// </summary>
    public class FunctionVerificationTests
    {
        private const string Folder = "Assets/__fnverify";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__fnverify");
            FunctionEvaluator.InvalidateAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A Function returning <paramref name="resultType"/>, or nothing when it is null.</summary>
        private static FunctionGraphAsset NewFunction(string assetName, System.Type resultType)
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

            if (resultType != null)
            {
                graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
                {
                    key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = resultType
                });
            }

            graph.PortDefinitionsChanged();
            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            return function;
        }

        private static void AddRead(FunctionGraphAsset function, string key)
        {
            var get = new Unity.VisualScripting.GetVariable
            {
                kind = Unity.VisualScripting.VariableKind.Object,
                position = new Vector2(-160.0f, 0.0f)
            };
            function.graph.units.Add(get);
            get.name.SetDefaultValue(key);
        }

        private static void AddWrite(FunctionGraphAsset function, string key)
        {
            var set = new Unity.VisualScripting.SetVariable
            {
                kind = Unity.VisualScripting.VariableKind.Object,
                position = new Vector2(-160.0f, 180.0f)
            };
            function.graph.units.Add(set);
            set.name.SetDefaultValue(key);
        }

        /// <summary>A saved tree with one Script Graph Variable node reading <paramref name="function"/>.</summary>
        private static (BehaviorTreeGraphAsset tree, VisualScriptGraphVariable node, string path) TreeReading(
            string treeName, FunctionGraphAsset function)
        {
            var path = $"{Folder}/{treeName}.asset";
            var tree = BehaviorTreeAuthoring.CreateTree(path);
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);

            if (function != null) node.SetFunction(function);

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            return (tree, node, path);
        }

        private static List<string> FunctionFindings(string treePath)
        {
            return BehaviorTreeVerification.Verify(treePath)
                .Where(f => f.Contains("Function") || f.Contains("orphaned sub-asset"))
                .ToList();
        }

        // ------------------------------------------------------------------ purity

        [Test]
        public void DeclaredPureFunction_ThatWrites_IsReportedWithTheWritingUnit()
        {
            var function = NewFunction("Impure", typeof(bool));
            AddWrite(function, "lastSeen");
            function.SetPure(true);
            EditorUtility.SetDirty(function);

            var findings = FunctionFindings(TreeReading("PurityTree", function).path);

            Assert.That(findings, Has.Some.Contains("declared pure but writes"),
                "a pure Function containing a write must be reported");
            Assert.That(findings, Has.Some.Contains("Impure"), "the report must name the Function");
            Assert.That(findings, Has.Some.Contains("lastSeen"), "the report must name the unit that writes");
        }

        [Test]
        public void FunctionDeclaredImpure_ThatWrites_IsNotReported()
        {
            var function = NewFunction("HonestlyImpure", typeof(bool));
            AddWrite(function, "lastSeen");
            function.SetPure(false);
            EditorUtility.SetDirty(function);

            var findings = FunctionFindings(TreeReading("ImpureTree", function).path);

            Assert.That(findings, Has.None.Contains("declared pure but writes"),
                "a Function that admits it writes is not a finding");
        }

        // ------------------------------------------------------------------ watched keys

        [Test]
        public void WatchedKey_DeclaredButNeverRead_IsReported()
        {
            var function = NewFunction("OverDeclared", typeof(bool));
            AddRead(function, "hp");
            function.SetWatchedKeys(new[] { "hp", "stamina" });
            EditorUtility.SetDirty(function);

            var findings = FunctionFindings(TreeReading("OverTree", function).path);

            Assert.That(findings, Has.Some.Contains("'stamina'").And.Some.Contains("never reads it"));
        }

        /// <summary>
        /// The sharp half. An empty watched-key list was always detectable; a <em>wrong</em> one produced a
        /// guard that never woke, with nothing to see.
        /// </summary>
        [Test]
        public void WatchedKey_ReadButNotDeclared_IsReportedWithTheConsequence()
        {
            var function = NewFunction("UnderDeclared", typeof(bool));
            AddRead(function, "hp");
            function.SetWatchedKeys(System.Array.Empty<string>());
            EditorUtility.SetDirty(function);

            var findings = FunctionFindings(TreeReading("UnderTree", function).path);

            Assert.That(findings, Has.Some.Contains("reads 'hp'"));
            Assert.That(findings, Has.Some.Contains("will not wake"),
                "the finding must say what it costs, not just that it is inconsistent");
        }

        [Test]
        public void WatchedKeys_MatchingTheGraph_AreNotReported()
        {
            var function = NewFunction("Consistent", typeof(bool));
            AddRead(function, "hp");
            function.SetWatchedKeys(new[] { "hp" });
            EditorUtility.SetDirty(function);

            var findings = FunctionFindings(TreeReading("ConsistentTree", function).path);

            Assert.That(findings, Is.Empty, string.Join(" | ", findings));
        }

        // ------------------------------------------------------------------ contract

        [Test]
        public void FunctionWithoutResultOutput_IsReportedByName()
        {
            var function = NewFunction("NoResult", null);
            EditorUtility.SetDirty(function);

            var findings = FunctionFindings(TreeReading("NoResultTree", function).path);

            Assert.That(findings, Has.Some.Contains("NoResult"));
            Assert.That(findings, Has.Some.Contains(FunctionGraphAsset.ResultKey));
        }

        [Test]
        public void NodeWithBothAFunctionAndAnEmbeddedGraph_IsReported()
        {
            var function = NewFunction("Winner", typeof(bool));
            AddRead(function, "hp");
            function.SetWatchedKeys(new[] { "hp" });

            var (tree, node, path) = TreeReading("AmbiguousTree", function);
            node.SetScriptGraph(BehaviorTreeAuthoring.CreateVariableReadGraph(tree, "hp", false));
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            var findings = FunctionFindings(path);

            Assert.That(findings, Has.Some.Contains("both a Function and an embedded graph"));
        }

        // ------------------------------------------------------------------ fn_extract

        [Test]
        public void Extract_MovesAnEmbeddedGraphIntoAProjectAssetAndRepointsTheNode()
        {
            var (tree, node, _) = TreeReading("ExtractTree", null);
            node.SetScriptGraph(BehaviorTreeAuthoring.CreateVariableReadGraph(tree, "hp", false));
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            Assert.That(node.EmbeddedScriptGraph, Is.Not.Null, "precondition: the node starts embedded");

            var extractedPath = $"{Folder}/Extracted.asset";
            var function = FunctionGraphAuthoring.ExtractToProjectAsset(tree, node, extractedPath);

            Assert.That(function, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(extractedPath), Is.Not.Null,
                "the Function must exist as a standalone project asset");
            Assert.That(node.Function, Is.SameAs(function), "the node must now read the Function");
            Assert.That(node.EmbeddedScriptGraph, Is.Null,
                "the embedded reference must be cleared, or the node reports as ambiguous");
            Assert.That(function.graph.units.Count, Is.GreaterThan(0), "the graph must have been copied, not emptied");
        }

        /// <summary>
        /// Extract copies rather than deletes, deliberately: deleting here would make this the second thing
        /// in the project that destroys graphs, which is exactly what the repository-removal sequencing
        /// forbids. The now-unreferenced sub-asset must therefore show up as an orphan instead.
        /// </summary>
        [Test]
        public void Extract_LeavesTheOriginalSubAsset_ForVerifyToReportAsAnOrphan()
        {
            var (tree, node, path) = TreeReading("OrphanTree", null);
            node.SetScriptGraph(BehaviorTreeAuthoring.CreateVariableReadGraph(tree, "hp", false));
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            FunctionGraphAuthoring.ExtractToProjectAsset(tree, node, $"{Folder}/Promoted.asset");

            Assert.That(FunctionFindings(path), Has.Some.Contains("orphaned sub-asset"),
                "the sub-asset the node no longer references must be reported, not silently left");
        }

        [Test]
        public void Extract_RefusesANodeWithNoEmbeddedGraph()
        {
            var function = NewFunction("AlreadyShared", typeof(bool));
            var (tree, node, _) = TreeReading("RefuseTree", function);

            Assert.That(() => FunctionGraphAuthoring.ExtractToProjectAsset(tree, node, $"{Folder}/Nope.asset"),
                Throws.ArgumentException);
        }

        [Test]
        public void Extract_RefusesToOverwriteAnExistingAsset()
        {
            var occupied = $"{Folder}/Occupied.asset";
            NewFunction("Occupied", typeof(bool));

            var (tree, node, _) = TreeReading("OverwriteTree", null);
            node.SetScriptGraph(BehaviorTreeAuthoring.CreateVariableReadGraph(tree, "hp", false));

            Assert.That(() => FunctionGraphAuthoring.ExtractToProjectAsset(tree, node, occupied),
                Throws.ArgumentException);
        }
    }
}

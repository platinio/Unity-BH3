using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The guard lints in <see cref="BehaviorTreeVerification"/>.
    ///
    /// <para>
    /// Every failure they report is silent at runtime — a guard that never wakes, or one that no longer
    /// interrupts — so the lint is the only thing standing between an author and a branch that quietly stops
    /// reacting. A lint that cannot fail is worth nothing, which is what these are for.
    /// </para>
    /// </summary>
    [TestFixture]
    public class GuardLintTests
    {
        private const string Folder = "Assets/__GuardLintTests";

        private string treePath;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__GuardLintTests");

            treePath = $"{Folder}/Tree.asset";
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        // ------------------------------------------------------------------ the variable-read Function

        /// <summary>
        /// Two guards in one tree reading the same variable share one Function asset. This is the behaviour
        /// that replaced minting a fresh anonymous sub-asset per guard, and it is the reason a Function has
        /// a path at all — so it is worth a test rather than a doc comment.
        /// </summary>
        [Test]
        public void TwoGuardsReadingOneVariable_ShareOneFunction()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(treePath);
            var first = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 200.0f);
            var second = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 200.0f, 200.0f);

            BehaviorTreeAuthoring.GuardOnVariable(asset, first, "hp", true, false, 0.0f, 100.0f);
            BehaviorTreeAuthoring.GuardOnVariable(asset, second, "hp", true, false, 200.0f, 100.0f);

            var functions = asset.graph.Nodes
                .OfType<VisualScriptGraphVariable>()
                .Select(node => node.Function)
                .Where(function => function != null)
                .ToList();

            Assert.That(functions.Count, Is.EqualTo(2), "both guards must have a condition");
            Assert.That(functions[0], Is.SameAs(functions[1]),
                "a second read of the same variable must reuse the asset, not mint a second identical one");
        }

        /// <summary>
        /// The cost of that reuse, made loud. The path is keyed on the variable and not on the fallback, so
        /// the second caller's fallback cannot be honoured — and handing back a Function whose fallback is
        /// not the one just asked for, silently, is the failure this warning exists to prevent.
        /// </summary>
        [Test]
        public void ASecondReadAskingForADifferentFallback_IsWarnedAndKeepsTheFirst()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(treePath);
            var first = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 200.0f);
            var second = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 200.0f, 200.0f);

            BehaviorTreeAuthoring.GuardOnVariable(asset, first, "hp", true, fallback: false, 0.0f, 100.0f);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "already reads 'hp' with a fallback of 'False'"));

            BehaviorTreeAuthoring.GuardOnVariable(asset, second, "hp", true, fallback: true, 200.0f, 100.0f);

            var function = asset.graph.Nodes.OfType<VisualScriptGraphVariable>().First().Function;
            var literal = function.graph.units.OfType<Unity.VisualScripting.Literal>().First();

            Assert.That(literal.value, Is.EqualTo(false),
                "the first caller's fallback is the one the shared asset keeps");
        }

        /// <summary>Entry -> Selector -> a guarded WaitTime, which is the shape every lint is about.</summary>
        /// <param name="conditionDeclaresKeys">
        /// Whether the guard's condition declares what it reads. <c>bt_guard_on_variable</c> builds a
        /// Function that does, and a guard reading one is re-seeded on save by <c>GuardScheduleSeeder</c> --
        /// so a test about a guard with no schedule has to opt out, or the thing it is testing is repaired
        /// underneath it.
        /// </param>
        private BehaviorTreeGraphAsset GuardedTree(
            out ConditionalExecution guard,
            BehaviorTreeAuthoring.GuardKind kind,
            bool conditionDeclaresKeys = true)
        {
            var asset = BehaviorTreeAuthoring.CreateTree(treePath);

            var selector = BehaviorTreeAuthoring.AddNode<Selector>(asset, 0.0f, 160.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, selector);

            var branch = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 360.0f);
            BehaviorTreeAuthoring.FeedFloat(asset, branch.Time, 1.0f, 0.0f, 540.0f);
            BehaviorTreeAuthoring.Connect(asset, selector, branch, 0);

            guard = conditionDeclaresKeys
                ? BehaviorTreeAuthoring.GuardOnVariable(asset, branch, "hasTarget", true, false, 0.0f, 260.0f, kind)
                : BehaviorTreeAuthoring.GuardOnFunction(
                    asset, branch,
                    Authoring.FunctionGraphAuthoring.CreateFunction($"{Folder}/DeclaresNothing.asset", typeof(bool)),
                    true, 0.0f, 260.0f, kind);

            BehaviorTreeAuthoring.Save(asset);

            return asset;
        }

        private static bool Reports(System.Collections.Generic.List<string> findings, string fragment) =>
            findings.Any(finding => finding.Contains(fragment));

        [Test]
        public void AGuardBuiltByTheHelperIsClean()
        {
            GuardedTree(out _, BehaviorTreeAuthoring.GuardKind.Reactive);

            var findings = BehaviorTreeVerification.Verify(treePath);

            Assert.IsFalse(Reports(findings, "watches no keys"),
                "GuardOnVariable seeds the key it was handed, so the default path must not trip its own lint");
            Assert.IsFalse(Reports(findings, "has no triggers"));
        }

        /// <summary>
        /// A conditional still gating a Selector branch is the migration case: it used to interrupt and
        /// silently no longer does.
        /// </summary>
        [Test]
        public void AnEntryOnlyGuardOnASelectorBranchIsReported()
        {
            GuardedTree(out _, BehaviorTreeAuthoring.GuardKind.Conditional);

            Assert.IsTrue(Reports(BehaviorTreeVerification.Verify(treePath), "is entry-only and gates a Selector branch"));
        }

        [Test]
        public void AGuardWithNoTriggersIsReported()
        {
            GuardedTree(out var guard, BehaviorTreeAuthoring.GuardKind.Reactive, conditionDeclaresKeys: false);
            ((ReactiveGuard)guard).ClearTriggers();
            BehaviorTreeAuthoring.Save(guard.graph.Nodes.Any() ? AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(treePath) : null);

            Assert.IsTrue(Reports(BehaviorTreeVerification.Verify(treePath), "has no triggers"));
        }

        /// <summary>
        /// A key list of blanks looks populated and can never mark the guard dirty, so the check has to count
        /// keys that could actually wake it rather than entries present.
        /// </summary>
        [Test]
        public void AKeyTriggerOfBlanksIsReportedAsWatchingNothing()
        {
            // The condition must declare nothing, or the blanks are covered by inherited keys and the guard
            // genuinely does wake -- which is the loosening step 2a made, not a hole in this lint.
            GuardedTree(out var guard, BehaviorTreeAuthoring.GuardKind.Reactive, conditionDeclaresKeys: false);

            var reactive = (ReactiveGuard)guard;
            reactive.ClearTriggers();

            var blank = GuardTrigger.KeyChanged();
            blank.Keys.Add("   ");
            reactive.AddTrigger(blank);

            BehaviorTreeAuthoring.Save(AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(treePath));

            Assert.IsTrue(Reports(BehaviorTreeVerification.Verify(treePath), "watches no keys"));
        }

        [Test]
        public void ABlankVariableNameIsRefusedAtAuthoringTime()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(treePath);
            var branch = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 360.0f);

            Assert.Throws<System.ArgumentException>(
                () => BehaviorTreeAuthoring.GuardOnVariable(asset, branch, "  ", true, false, 0.0f, 260.0f),
                "a guard that reads nothing can never become true, and as a reactive guard never wakes either");
        }

        /// <summary>
        /// The one a designer hits by accident: the fuzzy finder offers Unity's Set Variable first, and a
        /// fact written with it can never bump a version, so the guard watching that key sleeps forever with
        /// no error anywhere.
        /// </summary>
        [Test]
        public void AWatchedKeyWrittenByTheStockUnitIsReported()
        {
            var asset = GuardedTree(out _, BehaviorTreeAuthoring.GuardKind.Reactive);

            var writerNode = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(asset, 400.0f, 360.0f);
            writerNode.SetFunction(StockWriteFunction("hasTarget"));

            BehaviorTreeAuthoring.Save(asset);

            Assert.IsTrue(
                Reports(BehaviorTreeVerification.Verify(treePath), "stock Set Variable unit"),
                "the key the guard watches is written by a unit that cannot wake it");
        }

        [Test]
        public void AStockWriteToAKeyNobodyWatchesIsNotReported()
        {
            var asset = GuardedTree(out _, BehaviorTreeAuthoring.GuardKind.Reactive);

            var writerNode = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(asset, 400.0f, 360.0f);
            writerNode.SetFunction(StockWriteFunction("somethingElse"));

            BehaviorTreeAuthoring.Save(asset);

            Assert.IsFalse(
                Reports(BehaviorTreeVerification.Verify(treePath), "stock Set Variable unit"),
                "a stock write is only a problem when a guard is relying on seeing it");
        }

        /// <summary>A Function containing one stock Set Variable writing <paramref name="key"/>.</summary>
        private static ArcaneOnyx.VisualScriptingExtension.FunctionGraphAsset StockWriteFunction(string key)
        {
            var function = Authoring.FunctionGraphAuthoring.CreateFunction($"{Folder}/StockWrite{key}.asset");

            var stock = new Unity.VisualScripting.SetVariable { kind = Unity.VisualScripting.VariableKind.Object };
            function.graph.units.Add(stock);

            // The key as an inline value on the port, which is how a designer types it in.
            stock.name.SetDefaultValue(key);

            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();

            return function;
        }
    }
}

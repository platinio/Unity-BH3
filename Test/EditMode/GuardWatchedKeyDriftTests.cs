using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Unity-BH3#22: a guard's seeded watched keys never update when its Function's declaration changes.
    ///
    /// <para>
    /// Seeding writes the declared keys into the serialized <c>GuardTrigger.Keys</c>, and
    /// <c>SeedMissingGuardTriggers</c> never revisits a trigger that already exists — rewriting a schedule an
    /// author chose would be worse than the every-tick default it fixes. So the list freezes at the moment it
    /// was seeded.
    /// </para>
    ///
    /// <para>
    /// <b>Runtime behaviour is not the defect and is not what these test.</b> Inheritance unions the live
    /// declaration in, so the guard genuinely wakes on the new key. What these pin is that the divergence is
    /// <em>reported</em> and can be repaired, because the failure is an asset that describes a guard which no
    /// longer exists.
    /// </para>
    /// </summary>
    public class GuardWatchedKeyDriftTests
    {
        private const string Folder = "Assets/__GuardKeyDriftTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__GuardKeyDriftTests");
            FunctionEvaluator.InvalidateAll();
            NodeProblemCache.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
            NodeProblemCache.Invalidate();
        }

        private static FunctionGraphAsset Predicate(string assetName, params string[] watchedKeys)
        {
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = function.graph;

            var input = new ScriptGraphInput();
            var output = new ScriptGraphOutput();
            graph.units.Add(input);
            graph.units.Add(output);

            graph.controlInputDefinitions.Add(new ControlInputDefinition
            {
                key = FunctionGraphAsset.EnterKey, label = FunctionGraphAsset.EnterKey
            });
            graph.controlOutputDefinitions.Add(new ControlOutputDefinition
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
            function.SetWatchedKeys(watchedKeys);
            return function;
        }

        /// <summary>The reproduction from the issue: seed against `hp`, then grow the declaration.</summary>
        private static (BehaviorTreeGraphAsset tree, ReactiveGuard guard, FunctionGraphAsset function) Seeded()
        {
            var function = Predicate("IsHurt", "hp");

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var guard = (ReactiveGuard)BehaviorTreeAuthoring.GuardOnFunction(
                tree, owner, function, expected: true, 0.0f, 0.0f);

            return (tree, guard, function);
        }

        [Test]
        public void Seeding_WritesTheDeclaredKey()
        {
            var (_, guard, _) = Seeded();

            Assert.That(guard.Triggers.SelectMany(trigger => trigger.Keys), Contains.Item("hp"),
                "precondition: the guard was seeded from what the Function declared at authoring time");
        }

        [Test]
        public void AKeyDeclaredAfterSeeding_IsNotWrittenIntoTheTrigger()
        {
            var (_, guard, function) = Seeded();

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            FunctionEvaluator.InvalidateAll();

            Assert.That(guard.Triggers.SelectMany(trigger => trigger.Keys), Has.None.EqualTo("stamina"),
                "this is the defect in #22 -- the serialized list is frozen at the moment it was seeded");
        }

        // ------------------------------------------------------------------ it is reported

        [Test]
        public void TheDivergence_IsReportedOnTheNode()
        {
            var (_, guard, function) = Seeded();

            Assert.That(NodeProblemCache.For(guard), Is.Empty, "precondition: nothing wrong before the change");

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            FunctionEvaluator.InvalidateAll();

            var problems = NodeProblemCache.For(guard);

            Assert.That(problems.Select(problem => problem.Summary), Has.Some.Contains("stamina"),
                "the key the asset fails to mention is the one thing the report must name");
            Assert.That(problems.Select(problem => problem.Severity),
                Has.All.EqualTo(NodeProblemSeverity.Warning),
                "runtime behaviour is correct, so this is not an error");
        }

        [Test]
        public void TheDivergence_IsReportedByVerify()
        {
            var (tree, _, function) = Seeded();
            BehaviorTreeAuthoring.Save(tree);

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();
            FunctionEvaluator.InvalidateAll();

            var findings = BehaviorTreeVerification.Verify($"{Folder}/Tree.asset");

            Assert.That(findings, Has.Some.Contains("stamina"));
            Assert.That(findings, Has.Some.Contains("bt_refresh_guard_keys"), "name the repair");
        }

        // ------------------------------------------------------------------ it can be repaired

        [Test]
        public void RefreshingWatchedKeys_AddsTheDeclaredKeyAndClearsTheReport()
        {
            var (_, guard, function) = Seeded();

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            FunctionEvaluator.InvalidateAll();

            var added = guard.RefreshWatchedKeys();
            NodeProblemCache.Invalidate();

            Assert.That(added, Has.Some.Contains("stamina"), "the repair says what it changed");
            Assert.That(guard.Triggers.SelectMany(trigger => trigger.Keys), Contains.Item("stamina"));
            Assert.That(NodeProblemCache.For(guard), Is.Empty, "repaired, so the node must stop reporting");
        }

        /// <summary>
        /// The constraint that makes the repair safe to offer at all. A key the trigger lists that nothing
        /// declares may be a deliberate hand-typed one naming a fact written by a C# node no walk can see —
        /// so a refresh adds and never removes, exactly as the seeder never rewrites an existing schedule.
        /// </summary>
        [Test]
        public void RefreshingWatchedKeys_NeverRemovesAHandTypedKey()
        {
            var (_, guard, function) = Seeded();

            foreach (var trigger in guard.Triggers) trigger.Keys.Add("writtenByCSharp");

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            FunctionEvaluator.InvalidateAll();

            guard.RefreshWatchedKeys();

            Assert.That(guard.Triggers.SelectMany(trigger => trigger.Keys), Contains.Item("writtenByCSharp"),
                "a refresh that discarded a schedule somebody chose would be worse than the staleness it fixes");
        }

        [Test]
        public void AKeyTheConditionDoesNotDeclare_IsNotReported()
        {
            var (_, guard, _) = Seeded();

            foreach (var trigger in guard.Triggers) trigger.Keys.Add("writtenByCSharp");
            NodeProblemCache.Invalidate();

            Assert.That(NodeProblemCache.For(guard).Select(problem => problem.Summary),
                Has.None.Contains("writtenByCSharp"),
                "seeded and hand-typed keys are indistinguishable, so reporting this direction would fire on "
                + "most existing content");
        }

        // ------------------------------------------------------------------ the sharper case: undeclared reads

        /// <summary>
        /// The tool owner's scenario, and a worse bug than the one this file is named for.
        ///
        /// <para>
        /// Adding a variable <em>read</em> to a Function's graph does not change what the Function
        /// <em>declares</em>, and inheritance hands a guard the declaration. So the guard never wakes on the
        /// new fact — the branch silently stops firing — while every list involved agrees with every other
        /// list, which is why the drift check above stays quiet. Runtime is wrong here, not just the asset.
        /// </para>
        /// </summary>
        [Test]
        public void AKeyTheGraphReadsButDoesNotDeclare_IsReportedOnTheNodeHoldingTheFunction()
        {
            var function = Predicate("IsHurt", "hp");

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Reads.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            node.SetFunction(function);

            Assert.That(NodeProblemCache.For(node), Is.Empty, "precondition: declaration matches the graph");

            // What a designer does in the graph window: add a read, declare nothing.
            var get = new Unity.VisualScripting.GetVariable
            {
                kind = Unity.VisualScripting.VariableKind.Object, position = new Vector2(-300.0f, 0.0f)
            };
            function.graph.units.Add(get);
            get.Define();
            get.name.SetDefaultValue("stamina");

            FunctionEvaluator.InvalidateAll();
            NodeProblemCache.Invalidate();

            Assert.That(function.WatchedKeys, Has.None.EqualTo("stamina"),
                "the declaration does not move on its own -- that is the whole defect");

            var problems = NodeProblemCache.For(node);

            Assert.That(problems.Select(problem => problem.Summary), Has.Some.Contains("stamina"));
            Assert.That(problems.Select(problem => problem.Fix), Has.Some.Contains("on the Function"),
                "refreshing the guard's keys would not help -- it copies the declaration that is missing the "
                + "key -- so the fix named must be the one on the Function");
        }

        [Test]
        public void AKeyTheGraphReadsAndDeclares_IsNotReported()
        {
            var function = Predicate("IsHurt", "hp");

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Declared.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            node.SetFunction(function);

            var get = new Unity.VisualScripting.GetVariable
            {
                kind = Unity.VisualScripting.VariableKind.Object, position = new Vector2(-300.0f, 0.0f)
            };
            function.graph.units.Add(get);
            get.Define();
            get.name.SetDefaultValue("stamina");

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            FunctionEvaluator.InvalidateAll();
            NodeProblemCache.Invalidate();

            Assert.That(NodeProblemCache.For(node).Select(problem => problem.Summary),
                Has.None.Contains("stamina"),
                "declared and read agree, so there is nothing to say");
        }

        // ------------------------------------------------------------------ the narrow, provable case

        /// <summary>
        /// The case filed alongside #22: a guard whose condition is not connected reads its own port default
        /// forever, so it depends on nothing and every key it lists is provably stale. No false positives
        /// here, which is why it is reported where the general case is not.
        /// </summary>
        [Test]
        public void AGuardWithNoConditionConnected_IsReportedForWatchingAnything()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Disconnected.asset");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var guard = BehaviorTreeAuthoring.GuardOnVariable(tree, owner, "hp", true, false, 0.0f, 0.0f);

            foreach (var port in guard.valueInputs) port.Disconnect();
            NodeProblemCache.Invalidate();

            Assert.That(NodeProblemCache.For(guard).Select(problem => problem.Summary),
                Has.Some.Contains("Nothing is connected"));
        }

        [Test]
        public void AGuardWithAConnectedCondition_IsNotReportedForThat()
        {
            var (_, guard, _) = Seeded();

            Assert.That(NodeProblemCache.For(guard).Select(problem => problem.Summary),
                Has.None.Contains("Nothing is connected"));
        }
    }
}

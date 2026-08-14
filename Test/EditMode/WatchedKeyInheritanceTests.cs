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
    /// Covers what a guard learns from the Function its condition reads — spec 10, step 2a.
    ///
    /// <para>
    /// The subject of every test here is a <em>silent</em> failure. A guard whose watched keys are wrong does
    /// not throw, does not warn and does not stop: it returns a stale answer forever and the branch simply
    /// never fires. So each test asserts that the key set is right for a reason the author can point at, not
    /// merely that some evaluation happened.
    /// </para>
    /// </summary>
    public class WatchedKeyInheritanceTests
    {
        private const string Folder = "Assets/__watchedkeys";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__watchedkeys");
            FunctionEvaluator.InvalidateAll();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A saved predicate Function declaring <paramref name="watchedKeys"/> and reading them.</summary>
        private static FunctionGraphAsset NewPredicate(string assetName, params string[] watchedKeys)
        {
            return NewFunction(assetName, typeof(bool), watchedKeys);
        }

        private static FunctionGraphAsset NewFunction(string assetName, System.Type resultType, params string[] watchedKeys)
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

            // Declared and read, so the existing watched-key lint stays quiet and these tests are only ever
            // reporting on inheritance rather than on a Function that lies about itself.
            foreach (var key in watchedKeys.Distinct())
            {
                var get = new Unity.VisualScripting.GetVariable
                {
                    kind = Unity.VisualScripting.VariableKind.Object,
                    position = new Vector2(-160.0f, 0.0f)
                };
                graph.units.Add(get);
                get.name.SetDefaultValue(key);
            }

            function.SetWatchedKeys(watchedKeys);

            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            EditorUtility.SetDirty(function);
            return function;
        }

        private static (BehaviorTreeGraphAsset tree, string path) NewTree(string treeName)
        {
            var path = $"{Folder}/{treeName}.asset";
            return (BehaviorTreeAuthoring.CreateTree(path), path);
        }

        private static void Save(BehaviorTreeGraphAsset tree)
        {
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
        }

        private static GuardTrigger KeyTriggerOf(ConditionalExecution guard)
        {
            return guard.Triggers.FirstOrDefault(t => t != null && t.Kind == GuardTriggerKind.OnKeyChanged);
        }

        // ------------------------------------------------------------------ the walk

        [Test]
        public void AGuardReadingAFunctionDirectly_InheritsItsDeclaredKeys()
        {
            var (tree, _) = NewTree("Direct");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("IsHurt", "hp");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);

            Assert.That(InheritedWatchedKeys.Resolve(guard), Is.EquivalentTo(new[] { "hp" }));
        }

        [Test]
        public void AGuardReadingAFunctionThroughANot_StillInheritsItsKeys()
        {
            // The case a direct-connection-only walk would silently miss, and it is not exotic: expected:false
            // inserts a Not, so it is half of everything the authoring helpers produce.
            var (tree, _) = NewTree("Negated");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("IsHurt", "hp");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: false, 0.0f, 0.0f);

            Assert.That(InheritedWatchedKeys.Resolve(guard), Is.EquivalentTo(new[] { "hp" }),
                "a Not between the guard and its Function must not hide the Function's keys");
        }

        [Test]
        public void AFunctionDeclaringSeveralKeys_ContributesAllOfThem()
        {
            var (tree, _) = NewTree("Several");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("ShouldRetreat", "hp", "stamina", "hasCover");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);

            Assert.That(InheritedWatchedKeys.Resolve(guard),
                Is.EquivalentTo(new[] { "hp", "stamina", "hasCover" }));
        }

        [Test]
        public void ARepeatedKey_IsCollectedOnce()
        {
            var (tree, _) = NewTree("Duplicated");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            var function = NewPredicate("Repeats", "hp");
            function.SetWatchedKeys(new[] { "hp", "hp", " hp " });

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);

            Assert.That(InheritedWatchedKeys.Resolve(guard), Is.EqualTo(new[] { "hp" }),
                "a duplicated or whitespace-padded declaration is one key, not three");
        }

        [Test]
        public void AGuardReadingAnEmbeddedGraph_InheritsNothing()
        {
            // Declared, not derived. An embedded graph has no asset-level metadata to declare with, and
            // walking its units instead would quietly make "declares" and "happens to read" the same word.
            var (tree, _) = NewTree("Embedded");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            var guard = BehaviorTreeAuthoring.GuardOnVariable(tree, owner, "hasTarget", true, false, 0.0f, 0.0f);

            Assert.That(InheritedWatchedKeys.Resolve(guard), Is.Empty);
        }

        [Test]
        public void AGuardWithNothingConnected_InheritsNothing()
        {
            var (tree, _) = NewTree("Bare");
            var guard = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(tree, 0.0f, 0.0f);

            Assert.That(InheritedWatchedKeys.Resolve(guard), Is.Empty);
        }

        [Test]
        public void ResolvingANullGuard_YieldsNothingRatherThanThrowing()
        {
            // This runs on the guard evaluation path; a scheduling aid that can throw into the tree is worse
            // than no scheduling aid.
            Assert.That(InheritedWatchedKeys.Resolve(null), Is.Empty);
        }

        // ------------------------------------------------------------------ authoring-time seeding

        [Test]
        public void GuardOnFunction_SeedsAKeyTriggerFromTheDeclaredKeys()
        {
            var (tree, _) = NewTree("Seeded");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("IsHurt", "hp", "stamina");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);

            var trigger = KeyTriggerOf(guard);
            Assert.That(trigger, Is.Not.Null, "assigning a Function must leave a schedule an author can see");
            Assert.That(trigger.Keys, Is.EquivalentTo(new[] { "hp", "stamina" }));
        }

        [Test]
        public void GuardOnFunction_SeedsNormalisedKeys_NotTheRawDeclaration()
        {
            // The seed and the runtime walk must agree about the same declaration. They do so by being the
            // same walk -- this is what stops "how a declared key list is normalised" from having two
            // implementations free to disagree about a duplicate or a padded entry.
            var (tree, _) = NewTree("SeedNormalised");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            var function = NewPredicate("Repeats", "hp");
            function.SetWatchedKeys(new[] { "hp", "hp", " hp " });

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);

            Assert.That(KeyTriggerOf(guard).Keys, Is.EqualTo(new[] { "hp" }));
        }

        [Test]
        public void GuardOnFunction_SeedsThroughANot_SoANegatedGuardIsScheduledToo()
        {
            // Seeding happens after wiring, so the Not that expected:false inserts must not hide the keys --
            // the failure a seed read straight off the asset would never have caught.
            var (tree, _) = NewTree("SeedNegated");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("IsHurt", "hp");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: false, 0.0f, 0.0f);

            Assert.That(KeyTriggerOf(guard), Is.Not.Null);
            Assert.That(KeyTriggerOf(guard).Keys, Is.EquivalentTo(new[] { "hp" }));
        }

        [Test]
        public void GuardOnFunction_WithAFunctionDeclaringNoKeys_SeedsNoTrigger()
        {
            // Not an omission to fix by guessing an interval: the Function claims no agent-fact dependency,
            // so every-tick stays the only honest schedule and bt_verify says so.
            var (tree, _) = NewTree("Unkeyed");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("AlwaysTrue");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);

            Assert.That(guard.Triggers, Is.Empty);
        }

        [Test]
        public void GuardOnFunction_AsAnEntryOnlyDoorman_SeedsNoTrigger()
        {
            // A doorman is asked once and never again, so it has no schedule to seed.
            var (tree, _) = NewTree("Doorman");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("IsHurt", "hp");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(
                tree, owner, function, expected: true, 0.0f, 0.0f, BehaviorTreeAuthoring.GuardKind.Conditional);

            Assert.That(guard.HasRecomputeSchedule, Is.False);
            Assert.That(guard.Triggers, Is.Empty);
        }

        [Test]
        public void GuardOnFunction_WithANonBooleanFunction_IsRefusedAndNamesTheType()
        {
            var (tree, _) = NewTree("WrongType");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewFunction("Distance", typeof(float));

            var thrown = Assert.Throws<System.ArgumentException>(
                () => BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, true, 0.0f, 0.0f));

            Assert.That(thrown.Message, Does.Contain("Distance"), "the error must name the Function");
            Assert.That(thrown.Message, Does.Contain("Single").Or.Contain("float"),
                "the error must name what it returns instead");
        }

        [Test]
        public void GuardOnFunction_WithAFunctionDeclaringNoResult_IsRefused()
        {
            var (tree, _) = NewTree("NoResult");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewFunction("NoResult", null);

            Assert.Throws<System.ArgumentException>(
                () => BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, true, 0.0f, 0.0f));
        }

        // ------------------------------------------------------------------ verification

        private static List<string> GuardFindings(string treePath)
        {
            return BehaviorTreeVerification.Verify(treePath).Where(f => f.Contains("guard")).ToList();
        }

        [Test]
        public void AGuardWithNoTriggers_WhoseConditionDeclaresKeys_IsReportedWithThoseKeysNamed()
        {
            // The one gap authoring-time seeding cannot close: a Function assigned through the inspector runs
            // no authoring code, so the report has to be the safety net — and it is only actionable if it
            // names the keys.
            var (tree, path) = NewTree("Unseeded");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("IsHurt", "hp");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);
            ((ReactiveGuard)guard).ClearTriggers();
            Save(tree);

            var findings = GuardFindings(path);

            Assert.That(findings, Has.Some.Contains("no triggers"));
            Assert.That(findings, Has.Some.Contains("hp"), "the report must name the key the fix needs");
            Assert.That(findings, Has.Some.Contains("bt_guard_on_function"),
                "the report must name the command that seeds it");
        }

        [Test]
        public void AGuardWithNoTriggers_AndNoDeclaringCondition_KeepsTheOriginalAdvice()
        {
            var (tree, path) = NewTree("PlainUnseeded");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            var guard = BehaviorTreeAuthoring.GuardOnVariable(tree, owner, "hasTarget", true, false, 0.0f, 0.0f);
            ((ReactiveGuard)guard).ClearTriggers();
            Save(tree);

            var findings = GuardFindings(path);

            Assert.That(findings, Has.Some.Contains("no triggers"));
            Assert.That(findings, Has.None.Contains("bt_guard_on_function"),
                "a guard with nothing to inherit must not be told to use the Function command");
        }

        [Test]
        public void AnEmptyKeyTrigger_WhoseConditionDeclaresKeys_IsNotReportedAsWatchingNothing()
        {
            // Authoring a key trigger and letting the condition supply the keys is a legitimate shape now.
            // Reporting it as "watches no keys" would be a false alarm on the very pattern this feature adds.
            var (tree, path) = NewTree("InheritOnly");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var function = NewPredicate("IsHurt", "hp");

            var guard = BehaviorTreeAuthoring.GuardOnFunction(tree, owner, function, expected: true, 0.0f, 0.0f);
            KeyTriggerOf(guard).Keys.Clear();
            Save(tree);

            Assert.That(GuardFindings(path), Has.None.Contains("watches no keys"));
        }

        [Test]
        public void AnEmptyKeyTrigger_WithNothingToInherit_IsStillReportedAsWatchingNothing()
        {
            // The check this feature had to loosen must still fire where it always did, or loosening it
            // silently deleted a lint.
            var (tree, path) = NewTree("WatchesNothing");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            var guard = BehaviorTreeAuthoring.GuardOnVariable(tree, owner, "hasTarget", true, false, 0.0f, 0.0f);
            KeyTriggerOf(guard).Keys.Clear();
            Save(tree);

            Assert.That(GuardFindings(path), Has.Some.Contains("watches no keys"));
        }

        // ------------------------------------------------------------------ the trigger's key union

        [Test]
        public void ATriggerGivenInheritedKeys_ReportsThemSeparatelyFromAuthoredOnes()
        {
            // What the inspector, the dump and the cost display show. Merging the two lists would present a
            // schedule whose source an author cannot find.
            var trigger = GuardTrigger.KeyChanged("hasTarget");
            trigger.SetInheritedKeys(new[] { "hp" });

            Assert.That(trigger.InheritedKeys, Is.EquivalentTo(new[] { "hp" }));
            Assert.That(trigger.Keys, Is.EquivalentTo(new[] { "hasTarget" }),
                "inheriting must not rewrite what the author typed");

            var described = trigger.Describe();
            Assert.That(described, Does.Contain("hasTarget"));
            Assert.That(described, Does.Contain("hp"));
            Assert.That(described, Does.Contain("inherited"));
        }

        [Test]
        public void ATriggerWithOnlyInheritedKeys_DescribesItselfAsInheriting()
        {
            var trigger = GuardTrigger.KeyChanged();
            trigger.SetInheritedKeys(new[] { "hp" });

            Assert.That(trigger.Describe(), Does.Contain("hp").And.Contain("inherited"));
            Assert.That(trigger.Describe(), Does.Not.Contain("none"));
        }

        [Test]
        public void ATriggerGivenNullInheritedKeys_IsUnchangedRatherThanBroken()
        {
            var trigger = GuardTrigger.KeyChanged("hasTarget");
            trigger.SetInheritedKeys(null);

            Assert.That(trigger.InheritedKeys, Is.Empty);
            Assert.That(trigger.Describe(), Does.Contain("hasTarget"));
        }
    }
}

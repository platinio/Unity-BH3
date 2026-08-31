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
    /// Unity-BH3#24: a problem that has a one-click fix has to carry it, and running the carried repair has
    /// to actually fix the problem it came on.
    ///
    /// <para>
    /// The carried <see cref="NodeProblemRepair"/> is what lets the node inspector draw a button beside the
    /// defect, so these pin both halves of the contract: which problems carry which repair (and that
    /// prose-only problems carry none), and that <see cref="NodeProblemRepairs.Run"/> performs them the way
    /// the context menus always have. <see cref="DeclareWatchedKeyRepair"/> is asserted as data only — its
    /// performer opens a confirmation dialog, which a test cannot answer.
    /// </para>
    /// </summary>
    public class NodeProblemRepairTests
    {
        private const string Folder = "Assets/__NodeProblemRepairTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__NodeProblemRepairTests");
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

        // ------------------------------------------------------------------ fixtures

        private static FunctionGraphAsset Predicate(string assetName, string[] watchedKeys, params string[] inputs)
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

            foreach (var key in inputs)
            {
                graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
                {
                    key = key, label = key, type = typeof(float)
                });
            }

            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(bool)
            });

            graph.PortDefinitionsChanged();
            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            if (watchedKeys != null) function.SetWatchedKeys(watchedKeys);
            return function;
        }

        /// <summary>A GetVariable reading <paramref name="key"/> that the Function never declares.</summary>
        private static void AddUndeclaredRead(FunctionGraphAsset function, string key)
        {
            var read = new Unity.VisualScripting.GetVariable
            {
                kind = Unity.VisualScripting.VariableKind.Object
            };

            function.graph.units.Add(read);
            read.name.SetDefaultValue(key);

            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();
        }

        private static (BehaviorTreeGraphAsset tree, VisualScriptGraphVariable node) Drifted()
        {
            var function = Predicate("IsHurt", null, "threshold");
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            node.SetFunction(function);

            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "boost", label = "boost", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();
            FunctionEvaluator.InvalidateAll();

            return (tree, node);
        }

        private static (BehaviorTreeGraphAsset tree, ReactiveGuard guard, FunctionGraphAsset function) SeededGuard()
        {
            var function = Predicate("Guarded", new[] { "hp" });

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/GuardTree.asset");
            var owner = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            var guard = (ReactiveGuard)BehaviorTreeAuthoring.GuardOnFunction(
                tree, owner, function, expected: true, 0.0f, 0.0f);

            return (tree, guard, function);
        }

        private static NodeProblemRepair RepairOn(BehaviorTreeNode node, string labelFragment)
        {
            return NodeProblemCache.For(node)
                .Select(problem => problem.Repair)
                .FirstOrDefault(repair => repair != null && repair.Label.Contains(labelFragment));
        }

        // ------------------------------------------------------------------ which problems carry which repair

        [Test]
        public void ADriftedFunctionReader_CarriesARefreshPortsRepair()
        {
            var (_, node) = Drifted();

            Assert.That(RepairOn(node, "Refresh Ports"), Is.InstanceOf<RefreshContractPortsRepair>(),
                "the defect the inspector reports must arrive holding the button that fixes it");
        }

        [Test]
        public void ADriftedSubTreeCaller_CarriesARefreshParametersRepair()
        {
            var branch = BehaviorTreeAuthoring.CreateTree($"{Folder}/Branch.asset");
            BehaviorTreeAuthoring.Declare(branch, "attackRange", 0f, BehaviorTreeAuthoring.DeclarationScope.Required);
            BehaviorTreeAuthoring.Save(branch);

            var caller = BehaviorTreeAuthoring.CreateTree($"{Folder}/Caller.asset");
            var runNode = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0.0f, 200.0f);
            runNode.RefreshParameters();

            BehaviorTreeAuthoring.Declare(branch, "idleTime", 3.5f, BehaviorTreeAuthoring.DeclarationScope.Optional);
            BehaviorTreeAuthoring.Save(branch);
            NodeProblemCache.Invalidate();

            Assert.That(RepairOn(runNode, "Refresh Parameters"), Is.InstanceOf<RefreshContractPortsRepair>(),
                "the sub-tree caller drifts for the same reason the Function reader does and repairs the same way");
        }

        [Test]
        public void AnUndeclaredRead_CarriesADeclareRepairNamingTheFunctionAndTheKey()
        {
            var function = Predicate("Reads", null);
            AddUndeclaredRead(function, "mana");

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/ReadsTree.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            node.SetFunction(function);
            NodeProblemCache.Invalidate();

            var declare = RepairOn(node, "mana") as DeclareWatchedKeyRepair;

            Assert.That(declare, Is.Not.Null, "the undeclared read is the defect whose fix lives on another asset");
            Assert.That(declare.Function, Is.SameAs(function),
                "the repair must carry the asset to write on, or no surface can perform it");
            Assert.That(declare.Key, Is.EqualTo("mana"));
            Assert.That(declare.Label, Does.Contain("mana").And.Contain(function.name),
                "the button has to say what it writes and where, because the where is a shared asset");
        }

        [Test]
        public void AGuardsUndeclaredRead_CarriesTheSameDeclareRepair()
        {
            var (_, guard, function) = SeededGuard();
            AddUndeclaredRead(function, "stamina");
            NodeProblemCache.Invalidate();

            var declare = RepairOn(guard, "stamina") as DeclareWatchedKeyRepair;

            Assert.That(declare, Is.Not.Null,
                "the guard is the node that silently stops firing, so it must offer the repair too");
            Assert.That(declare.Function, Is.SameAs(function));
        }

        [Test]
        public void AGuardsTriggerDrift_CarriesARefreshWatchedKeysRepair()
        {
            var (_, guard, function) = SeededGuard();

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            FunctionEvaluator.InvalidateAll();

            Assert.That(RepairOn(guard, "Refresh Watched Keys"), Is.InstanceOf<RefreshWatchedKeysRepair>());
        }

        [Test]
        public void AnUnfedRequiredPort_CarriesNoRepair()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Move.asset");
            var move = BehaviorTreeAuthoring.AddNode<SetNavAgentPosition>(tree, 0.0f, 0.0f);

            var problems = NodeProblemCache.For(move);

            Assert.That(problems, Is.Not.Empty,
                "the fixture must actually raise the problem, or Has.All.Null passes on an empty list");
            Assert.That(problems.Select(problem => problem.Repair), Has.All.Null,
                "wiring a port is canvas work; a button that cannot do it must not exist");
        }

        // ------------------------------------------------------------------ running a carried repair

        [Test]
        public void RunningTheRefreshPortsRepair_FixesTheDriftItArrivedOn()
        {
            var (tree, node) = Drifted();

            var repair = RepairOn(node, "Refresh Ports");
            Assert.That(repair, Is.Not.Null, "precondition: the drift is reported with its repair");

            Assert.That(NodeProblemRepairs.Run(node, repair, tree), Is.True);

            Assert.That(NodeProblemCache.For(node).Select(problem => problem.Fix), Has.None.Contains("Refresh"),
                "the runner must leave the cache telling the truth, not wait for the next import");
        }

        [Test]
        public void RunningTheRefreshWatchedKeysRepair_WritesTheMissingKey()
        {
            var (tree, guard, function) = SeededGuard();

            function.SetWatchedKeys(new[] { "hp", "stamina" });
            FunctionEvaluator.InvalidateAll();

            var repair = RepairOn(guard, "Refresh Watched Keys");
            Assert.That(repair, Is.Not.Null, "precondition: the trigger drift is reported with its repair");

            Assert.That(NodeProblemRepairs.Run(guard, repair, tree), Is.True);

            Assert.That(guard.Triggers.SelectMany(trigger => trigger.Keys), Contains.Item("stamina"));
            Assert.That(NodeProblemCache.For(guard).Select(problem => problem.Fix),
                Has.None.Contains("Refresh Watched Keys"));
        }

        [Test]
        public void ARepairTheNodeCannotPerform_IsRefusedRatherThanHalfDone()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Refused.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemRepairs.Run(wait, new RefreshWatchedKeysRepair(), tree), Is.False);
            Assert.That(NodeProblemRepairs.Run(wait, new RefreshContractPortsRepair("Refresh Ports"), tree), Is.False);
        }

        // ------------------------------------------------------------------ the badge's half

        [Test]
        public void TheTooltip_PointsAtTheInspectorWhenARepairExists()
        {
            var (_, node) = Drifted();

            Assert.That(NodeProblemCache.DescriptionOf(node), Does.Contain("inspector"),
                "the badge is where a designer learns the fix is one selection away");
        }

        [Test]
        public void TheTooltip_StaysQuietWhenNoProblemCarriesARepair()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Quiet.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemCache.DescriptionOf(wait), Does.Not.Contain("inspector"),
                "pointing at an inspector with no button in it would be the old misleading badge again");
        }
    }
}

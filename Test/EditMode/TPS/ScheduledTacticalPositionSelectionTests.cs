using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityTacticalPositionSelection;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    [TestFixture]
    public class ScheduledTacticalPositionSelectionTests
    {
        private const int MaxFrames = 100;
        private const string FlankTargetQueryGuid = "65e32a658e111cf468590c3743d64cbc";

        private static readonly Vector3 TargetSixMetresAhead = new(0f, 0f, 6f);

        private BehaviorTreeGraph graph;
        private TacticalPositionSelectionQueryItem item;

        [SetUp]
        public void SetUp()
        {
            graph = new BehaviorTreeGraph();

            var function = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(AssetDatabase.GUIDToAssetPath(FlankTargetQueryGuid));
            Assert.IsNotNull(function, "The TPS GeneratorGallery demo's FlankTargetQuery, borrowed as this fixture's query, is missing.");

            item = ScriptableObject.CreateInstance<TacticalPositionSelectionQueryItem>();
            item.name = "Scheduled node test query";
            item.Query.SetFunction(function);
        }

        [TearDown]
        public void TearDown()
        {
            TPSScheduler.Shared.CancelAll();
            Object.DestroyImmediate(item);
        }

        [Test]
        public void ItRunsUntilTheSchedulerAnswers_ThenSucceeds_AndOffersTheWinner()
        {
            var (node, outputs) = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            Assert.AreEqual(ExecutionStatus.Running, node.OnUpdateInternal(), "Nothing ticked the scheduler yet.");

            Assert.AreEqual(ExecutionStatus.Success, TickSchedulerUntilNodeFinishes(node));
            Assert.IsTrue(outputs.HasPosition.Read());
            Assert.AreNotEqual(Vector3.zero, outputs.SelectedPosition.Read(),
                "The borrowed FlankTargetQuery's arc never includes the origin, so the winner can't be zero.");
        }

        [Test]
        public void AWinnerBelowTheMinimumScore_Fails_AndOffersNoPosition()
        {
            var (node, outputs) = ScheduledNode(minimumScore: 2f);

            node.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Failure, TickSchedulerUntilNodeFinishes(node), "Scores never exceed 1.");
            Assert.IsFalse(outputs.HasPosition.Read());
            Assert.AreEqual(Vector3.zero, outputs.SelectedPosition.Read(), "A rejected winner must not reach the tree.");
        }

        [Test]
        public void AQueryTheSchedulerCancels_FailsTheNode()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            TPSScheduler.Shared.CancelAll();

            Assert.AreEqual(ExecutionStatus.Failure, node.OnUpdateInternal());
        }

        [Test]
        public void LeavingTheNodeWhileItRuns_CancelsTheQuery()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);
            int pendingBefore = TPSScheduler.Shared.PendingCount;

            node.OnNodeEnter();
            Assert.AreEqual(pendingBefore + 1, TPSScheduler.Shared.PendingCount);

            node.OnNodeExit();

            Assert.AreEqual(pendingBefore, TPSScheduler.Shared.PendingCount);
        }

        [Test]
        public void EnteringAgainWithoutLeaving_ReplacesTheQueryInFlight()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);
            int pendingBefore = TPSScheduler.Shared.PendingCount;

            node.OnNodeEnter();
            node.OnNodeEnter();

            Assert.AreEqual(pendingBefore + 1, TPSScheduler.Shared.PendingCount, "The first query must be dropped.");
            Assert.AreEqual(ExecutionStatus.Success, TickSchedulerUntilNodeFinishes(node));
        }

        [Test]
        public void ReleasingTheTree_CancelsTheQueryInFlight()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);
            int pendingBefore = TPSScheduler.Shared.PendingCount;

            node.OnNodeEnter();
            node.OnDestroy();

            Assert.AreEqual(pendingBefore, TPSScheduler.Shared.PendingCount);
        }

        [Test]
        public void TheQuery_IsBuiltOnce_AndReusedForTheNextDecision()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            TickSchedulerUntilNodeFinishes(node);
            var firstBuild = node.BuiltQuery;

            node.OnNodeEnter();
            TickSchedulerUntilNodeFinishes(node);

            Assert.IsNotNull(firstBuild);
            Assert.AreSame(firstBuild, node.BuiltQuery, "A second decision by the same agent must reuse its query.");
        }

        [Test]
        public void EditingThePreset_BuildsTheQueryAgain()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            TickSchedulerUntilNodeFinishes(node);
            var firstBuild = node.BuiltQuery;

            item.Query.Invalidate();
            node.OnNodeEnter();

            Assert.AreNotSame(firstBuild, node.BuiltQuery, "An edited preset must not keep running the old query.");
        }

        [Test]
        public void ReleasingTheTree_DisposesTheBuiltQuery()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            TickSchedulerUntilNodeFinishes(node);
            var built = node.BuiltQuery;

            node.OnDestroy();

            Assert.Throws<System.ObjectDisposedException>(() => built.SelectPosition(new StandingAgent(), Vector3.zero),
                "The query's evaluators must be back in the pool.");
        }

        [Test]
        public void ANodeWithNoQuery_ReportsAProblemOnTheCanvas()
        {
            var node = AddNode<ScheduledTacticalPositionSelection>();
            var problems = new List<NodeProblem>();

            node.CollectProblems(problems);

            Assert.IsTrue(problems.Exists(problem => problem.Severity == NodeProblemSeverity.Error),
                "A node with no preset must say so before Play.");
        }

        private (ScheduledTacticalPositionSelection node, Outputs outputs) ScheduledNode(float minimumScore)
        {
            var agent = AddNode<ValueSource<ITacticalAgent>>();
            agent.Published = new StandingAgent();
            var target = AddNode<ValueSource<Vector3>>();
            target.Published = TargetSixMetresAhead;

            var node = AddNode<ScheduledTacticalPositionSelection>();
            node.Query.SetDefaultValue(item);
            node.MinimumScore.SetDefaultValue(minimumScore);
            agent.Value.ValidlyConnectTo(node.Evaluator);
            target.Value.ValidlyConnectTo(node.TargetPosition);

            var hasPosition = AddNode<ValueReader<bool>>();
            node.HasPosition.ValidlyConnectTo(hasPosition.Input);
            var selectedPosition = AddNode<ValueReader<Vector3>>();
            node.SelectedPosition.ValidlyConnectTo(selectedPosition.Input);

            return (node, new Outputs(hasPosition, selectedPosition));
        }

        // Ports only exist once the node is in a graph: Nodes.Add fires AfterAdd -> Define().
        private T AddNode<T>() where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(0f, 0f, 150f, 100f) };
            graph.Nodes.Add(node);
            return node;
        }

        private static ExecutionStatus TickSchedulerUntilNodeFinishes(ScheduledTacticalPositionSelection node)
        {
            for (int frame = 0; frame < MaxFrames; frame++)
            {
                TPSScheduler.Shared.Tick();

                var status = node.OnUpdateInternal();
                if (status != ExecutionStatus.Running) return status;
            }

            Assert.Fail("The scheduled query never answered.");
            return ExecutionStatus.Running;
        }

        private readonly struct Outputs
        {
            public readonly ValueReader<bool> HasPosition;
            public readonly ValueReader<Vector3> SelectedPosition;

            public Outputs(ValueReader<bool> hasPosition, ValueReader<Vector3> selectedPosition)
            {
                HasPosition = hasPosition;
                SelectedPosition = selectedPosition;
            }
        }

        private sealed class StandingAgent : ITacticalAgent
        {
            public Vector3 Position => Vector3.zero;

            public Vector3 Forward => Vector3.forward;
        }

        private sealed class ValueSource<T> : BehaviorTreeNode
        {
            public T Published { get; set; }

            [DoNotSerialize]
            public ValueOutput Value { get; private set; }

            public override string NodeName => $"Test {typeof(T).Name} Source";

            protected override void Definition()
            {
                base.Definition();

                Value = ValueOutput<T>(nameof(Value), () => Published);
            }
        }

        private sealed class ValueReader<T> : BehaviorTreeNode
        {
            [DoNotSerialize]
            public ValueInput Input { get; private set; }

            public override string NodeName => $"Test {typeof(T).Name} Reader";

            public T Read() => Input.GetValue<T>();

            protected override void Definition()
            {
                base.Definition();

                Input = ValueInput<T>(nameof(Input));
            }
        }
    }
}

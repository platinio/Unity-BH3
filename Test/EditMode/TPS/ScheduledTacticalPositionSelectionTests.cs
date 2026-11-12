using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityTacticalPositionSelection;
using NUnit.Framework;
using UnityEngine;
using static ArcaneOnyx.BehaviorTree.Tests.TPSNodeTestDoubles;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    [TestFixture]
    public class ScheduledTacticalPositionSelectionTests
    {
        private const int MaxFrames = 100;

        private static readonly Vector3 TargetSixMetresAhead = new(0f, 0f, 6f);

        private BehaviorTreeGraph graph;
        private TacticalPositionSelectionQueryItem item;

        [SetUp]
        public void SetUp()
        {
            graph = new BehaviorTreeGraph();
            item = CreateFlankItem();
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
            var firstBuild = node.BuiltInstance;

            node.OnNodeEnter();
            TickSchedulerUntilNodeFinishes(node);

            Assert.IsNotNull(firstBuild);
            Assert.AreSame(firstBuild, node.BuiltInstance, "A second decision by the same agent must reuse its query.");
        }

        [Test]
        public void EditingThePreset_BuildsTheQueryAgain()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            TickSchedulerUntilNodeFinishes(node);
            var firstBuild = node.BuiltInstance;

            item.Query.Invalidate();
            node.OnNodeEnter();

            Assert.AreNotSame(firstBuild, node.BuiltInstance, "An edited preset must not keep running the old query.");
        }

        [Test]
        public void ReleasingTheTree_DisposesTheBuiltQuery()
        {
            var (node, _) = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            TickSchedulerUntilNodeFinishes(node);
            var built = node.BuiltInstance;

            node.OnDestroy();

            Assert.Throws<ObjectDisposedException>(() => built.Run(new PositionQueryRequest(Vector3.zero)),
                "The query's evaluators must be back in the pool.");
        }

        [Test]
        public void ACodeSource_IsScheduledAndAnswers_LikeAGraphOne()
        {
            var nearTheTarget = new Vector3(0f, 0f, 5f);
            var source = new CountingCodeSource(new Vector3(0f, 0f, -10f), nearTheTarget);
            var (node, outputs) = ScheduledNode(minimumScore: 0f, source);

            node.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, TickSchedulerUntilNodeFinishes(node));
            Assert.AreEqual(nearTheTarget, outputs.SelectedPosition.Read());
            Assert.AreEqual(1, source.Builds);
        }

        [Test]
        public void APresetPickedInlineOnThePort_BeforeItTookSources_StillRuns()
        {
            var agent = AddNode<ValueSource<ITacticalAgent>>();
            agent.Published = new StandingAgent();
            var node = AddNodeWithInlinePreset<ScheduledTacticalPositionSelection>(graph, item);
            agent.Value.ValidlyConnectTo(node.Evaluator);

            node.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, TickSchedulerUntilNodeFinishes(node));
        }

        [Test]
        public void ANodeWithNoQuery_ReportsAProblemOnTheCanvas()
        {
            var node = AddNode<ScheduledTacticalPositionSelection>();
            var problems = new List<NodeProblem>();

            node.CollectProblems(problems);

            Assert.IsTrue(problems.Exists(IsNoQueryProblem), "A node with no preset must say so before Play.");
        }

        private (ScheduledTacticalPositionSelection node, Outputs outputs) ScheduledNode(
            float minimumScore, IPositionQuerySource source = null)
        {
            var agent = AddNode<ValueSource<ITacticalAgent>>();
            agent.Published = new StandingAgent();
            var target = AddNode<ValueSource<Vector3>>();
            target.Published = TargetSixMetresAhead;

            var query = AddNode<ValueSource<IPositionQuerySource>>();
            query.Published = source ?? item;

            var node = AddNode<ScheduledTacticalPositionSelection>();
            query.Value.ValidlyConnectTo(node.Query);
            node.MinimumScore.SetDefaultValue(minimumScore);
            agent.Value.ValidlyConnectTo(node.Evaluator);
            target.Value.ValidlyConnectTo(node.TargetPosition);

            var hasPosition = AddNode<ValueReader<bool>>();
            node.HasPosition.ValidlyConnectTo(hasPosition.Input);
            var selectedPosition = AddNode<ValueReader<Vector3>>();
            node.SelectedPosition.ValidlyConnectTo(selectedPosition.Input);

            return (node, new Outputs(hasPosition, selectedPosition));
        }

        private T AddNode<T>() where T : BehaviorTreeNode, new() => TPSNodeTestDoubles.AddNode<T>(graph);

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

        private static bool IsNoQueryProblem(NodeProblem problem)
            => problem.Severity == NodeProblemSeverity.Error && problem.Summary.Contains("No query connected");

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
    }
}

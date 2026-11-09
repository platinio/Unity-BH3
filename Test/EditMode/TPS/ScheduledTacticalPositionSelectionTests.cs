using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityTacticalPositionSelection;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The scheduled TPS node runs until the shared scheduler answers, then succeeds or fails on the winner's
    /// score, and leaving it early cancels the query. The scheduler is ticked by hand, as play mode would.
    /// </summary>
    [TestFixture]
    public class ScheduledTacticalPositionSelectionTests
    {
        private const int MaxFrames = 100;
        private const string FlankTargetQueryGuid = "65e32a658e111cf468590c3743d64cbc";

        private BehaviorTreeGraph graph;
        private TacticalPositionSelectionQueryItem item;

        [SetUp]
        public void SetUp()
        {
            graph = new BehaviorTreeGraph();

            item = ScriptableObject.CreateInstance<TacticalPositionSelectionQueryItem>();
            item.name = "Scheduled node test query";
            var path = AssetDatabase.GUIDToAssetPath(FlankTargetQueryGuid);
            item.Query.SetFunction(AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(path));
        }

        [TearDown]
        public void TearDown()
        {
            TPSScheduler.Shared.CancelAll();
            Object.DestroyImmediate(item);
        }

        [Test]
        public void ItRunsUntilTheSchedulerAnswers_ThenSucceeds()
        {
            var node = ScheduledNode(minimumScore: 0f);

            node.OnNodeEnter();
            Assert.AreEqual(ExecutionStatus.Running, node.OnUpdateInternal(), "Nothing ticked the scheduler yet.");

            Assert.AreEqual(ExecutionStatus.Success, TickUntilFinished(node));
        }

        [Test]
        public void AWinnerBelowTheMinimumScore_Fails()
        {
            var node = ScheduledNode(minimumScore: 2f);

            node.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Failure, TickUntilFinished(node), "Scores never exceed 1.");
        }

        [Test]
        public void LeavingTheNodeWhileItRuns_CancelsTheQuery()
        {
            var node = ScheduledNode(minimumScore: 0f);
            int pendingBefore = TPSScheduler.Shared.PendingCount;

            node.OnNodeEnter();
            Assert.AreEqual(pendingBefore + 1, TPSScheduler.Shared.PendingCount);

            node.OnNodeExit();

            Assert.AreEqual(pendingBefore, TPSScheduler.Shared.PendingCount);
        }

        private ScheduledTacticalPositionSelection ScheduledNode(float minimumScore)
        {
            var agent = AddNode<ValueSource<ITacticalAgent>>();
            agent.Published = new StandingAgent();
            var target = AddNode<ValueSource<Vector3>>();
            target.Published = new Vector3(0f, 0f, 6f);

            var node = AddNode<ScheduledTacticalPositionSelection>();
            node.Query.SetDefaultValue(item);
            node.MinimumScore.SetDefaultValue(minimumScore);
            agent.Value.ValidlyConnectTo(node.Evaluator);
            target.Value.ValidlyConnectTo(node.TargetPosition);

            return node;
        }

        // Ports only exist once the node is in a graph: Nodes.Add fires AfterAdd -> Define().
        private T AddNode<T>() where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(0f, 0f, 150f, 100f) };
            graph.Nodes.Add(node);
            return node;
        }

        private static ExecutionStatus TickUntilFinished(ScheduledTacticalPositionSelection node)
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
    }
}

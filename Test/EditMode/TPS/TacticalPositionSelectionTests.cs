using System;
using System.Collections.Generic;
using System.Reflection;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityTacticalPositionSelection;
using NUnit.Framework;
using UnityEngine;
using static ArcaneOnyx.BehaviorTree.Tests.TPSNodeTestDoubles;
using Object = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>The immediate TPS data node: a query source on its port, built once per agent, run on every pull.</summary>
    [TestFixture]
    public class TacticalPositionSelectionTests
    {
        private static readonly Vector3 TargetSixMetresAhead = new(0f, 0f, 6f);
        private static readonly Vector3 SpotNearTheTarget = new(0f, 0f, 5f);
        private static readonly Vector3 SpotBehindTheAgent = new(0f, 0f, -10f);

        private BehaviorTreeGraph graph;
        private TacticalPositionSelectionQueryItem item;

        [SetUp]
        public void SetUp()
        {
            graph = new BehaviorTreeGraph();
            item = CreateFlankItem();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(item);

        [Test]
        public void ACodeSourceOnThePort_GivesTheWinnerOfItsQuery()
        {
            var node = SelectionNode();
            Connect(new CountingCodeSource(SpotBehindTheAgent, SpotNearTheTarget), node);
            var (hasPosition, selectedPosition) = ReadOutputs(node);

            Assert.IsTrue(hasPosition.Read());
            Assert.AreEqual(SpotNearTheTarget, selectedPosition.Read());
        }

        [Test]
        public void AQueryPickedOnATPSQueryLiteral_RunsThroughTheNode()
        {
            var node = SelectionNode();
            var literal = AddNode<TPSQueryLiteral>();
            SetPrivateField(literal, "value", item);
            literal.Value.ValidlyConnectTo(node.Query);
            var (hasPosition, selectedPosition) = ReadOutputs(node);

            Assert.IsTrue(hasPosition.Read());
            Assert.AreNotEqual(Vector3.zero, selectedPosition.Read(),
                "The borrowed FlankTargetQuery's arc never includes the origin, so the winner can't be zero.");
        }

        [Test]
        public void TheNode_BuildsItsQueryOnce_AcrossFrames()
        {
            var node = SelectionNode();
            var source = new CountingCodeSource(SpotBehindTheAgent, SpotNearTheTarget);
            Connect(source, node);
            var (_, selectedPosition) = ReadOutputs(node);

            selectedPosition.Read();
            var firstBuild = node.BuiltInstance;
            node.ForgetThisFramesSelection();
            selectedPosition.Read();

            Assert.AreEqual(1, source.Builds);
            Assert.AreSame(firstBuild, node.BuiltInstance);
        }

        [Test]
        public void ReleasingTheTree_DisposesTheBuiltQuery()
        {
            var node = SelectionNode();
            Connect(new CountingCodeSource(SpotBehindTheAgent, SpotNearTheTarget), node);
            ReadOutputs(node).selectedPosition.Read();
            var built = node.BuiltInstance;

            node.OnDestroy();

            Assert.Throws<ObjectDisposedException>(() => built.Run(new PositionQueryRequest(Vector3.zero)));
        }

        [Test]
        public void AQueryPickedBeforeTheQueryWasAPort_StillRuns()
        {
            var node = SelectionNode();
            SetPrivateField(node, "query", item);
            var (hasPosition, _) = ReadOutputs(node);

            Assert.IsTrue(hasPosition.Read());
        }

        [Test]
        public void AQueryItemWiredFromAnOutputOfItsOwnType_StillRuns()
        {
            var node = SelectionNode();
            var itemOutput = AddNode<ValueSource<TacticalPositionSelectionQueryItem>>();
            itemOutput.Published = item;
            itemOutput.Value.ValidlyConnectTo(node.Query);
            var (hasPosition, _) = ReadOutputs(node);

            Assert.IsTrue(hasPosition.Read(), "Trees that wired an item into the node must keep working.");
        }

        [Test]
        public void APresetPickedInlineOnThePort_BeforeItTookSources_StillRuns()
        {
            var node = AddNodeWithInlinePreset<TacticalPositionSelection>(graph, item);
            var agent = AddNode<ValueSource<ITacticalAgent>>();
            agent.Published = new StandingAgent();
            agent.Value.ValidlyConnectTo(node.Evaluator);
            var (hasPosition, _) = ReadOutputs(node);

            Assert.IsTrue(hasPosition.Read());
        }

        [Test]
        public void AnInlinePreset_ReplacesAnOlderPresetPickedBeforeTheQueryWasAPort()
        {
            var older = CreateFlankItem();
            try
            {
                var node = new TacticalPositionSelection { Position = new Rect(0f, 0f, 150f, 100f) };
                SetPrivateField(node, "query", older);
                node.defaultValues["Query"] = item;
                graph.Nodes.Add(node);

                Assert.AreSame(item, GetPrivateField(node, "query"));
            }
            finally
            {
                Object.DestroyImmediate(older);
            }
        }

        [Test]
        public void ADeletedQueryItem_IsReportedAsNoQuery_NotRun()
        {
            var node = SelectionNode();
            var deleted = CreateFlankItem();
            Object.DestroyImmediate(deleted);
            var itemOutput = AddNode<ValueSource<TacticalPositionSelectionQueryItem>>();
            itemOutput.Published = deleted;
            itemOutput.Value.ValidlyConnectTo(node.Query);
            var (hasPosition, _) = ReadOutputs(node);

            var exception = Assert.Throws<InvalidOperationException>(() => hasPosition.Read());
            StringAssert.Contains("has no query", exception.Message);
        }

        [Test]
        public void ATPSQueryLiteralHoldingADeletedItem_OffersARealNull()
        {
            var deleted = CreateFlankItem();
            Object.DestroyImmediate(deleted);
            var literal = AddNode<TPSQueryLiteral>();
            SetPrivateField(literal, "value", deleted);
            var reader = AddNode<ValueReader<IPositionQuerySource>>();
            literal.Value.ValidlyConnectTo(reader.Input);

            Assert.IsTrue(ReferenceEquals(reader.Read(), null), "A fake-null asset would pass any consumer's null check.");
        }

        [Test]
        public void APresetSavedBeforeTheQueryTookSources_IsShownAsAWarningOnTheCanvas()
        {
            var node = AddNodeWithInlinePreset<TacticalPositionSelection>(graph, item);
            var problems = new List<NodeProblem>();

            node.CollectProblems(problems);

            Assert.IsTrue(problems.Exists(problem =>
                problem.Severity == NodeProblemSeverity.Warning && problem.Summary.Contains("Runs preset")));
        }

        [Test]
        public void ANodeWithNothingOnItsQueryPort_ReportsAProblemOnTheCanvas()
        {
            var node = SelectionNode();
            var problems = new List<NodeProblem>();

            node.CollectProblems(problems);

            Assert.IsTrue(problems.Exists(problem =>
                problem.Severity == NodeProblemSeverity.Error && problem.Summary.Contains("No query connected")));
        }

        private TacticalPositionSelection SelectionNode()
        {
            var agent = AddNode<ValueSource<ITacticalAgent>>();
            agent.Published = new StandingAgent();
            var target = AddNode<ValueSource<Vector3>>();
            target.Published = TargetSixMetresAhead;

            var node = AddNode<TacticalPositionSelection>();
            agent.Value.ValidlyConnectTo(node.Evaluator);
            target.Value.ValidlyConnectTo(node.TargetPosition);
            return node;
        }

        private void Connect(IPositionQuerySource source, TacticalPositionSelection node)
        {
            var query = AddNode<ValueSource<IPositionQuerySource>>();
            query.Published = source;
            query.Value.ValidlyConnectTo(node.Query);
        }

        private (ValueReader<bool> hasPosition, ValueReader<Vector3> selectedPosition) ReadOutputs(
            TacticalPositionSelection node)
        {
            var hasPosition = AddNode<ValueReader<bool>>();
            node.HasPosition.ValidlyConnectTo(hasPosition.Input);
            var selectedPosition = AddNode<ValueReader<Vector3>>();
            node.SelectedPosition.ValidlyConnectTo(selectedPosition.Input);
            return (hasPosition, selectedPosition);
        }

        private T AddNode<T>() where T : BehaviorTreeNode, new() => TPSNodeTestDoubles.AddNode<T>(graph);

        private static void SetPrivateField(object target, string fieldName, object value)
            => PrivateField(target, fieldName).SetValue(target, value);

        private static object GetPrivateField(object target, string fieldName)
            => PrivateField(target, fieldName).GetValue(target);

        private static FieldInfo PrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name} has no field '{fieldName}'.");
            return field;
        }
    }
}

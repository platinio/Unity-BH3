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
    /// A node that is wrong before anyone runs it has to say so on the canvas.
    ///
    /// <para>
    /// Contract drift, an unfed required port and a missing reference were all invisible until Play threw,
    /// which meant a healthy-looking node for a tree that could not work. These cover both halves: that a
    /// node reports what is wrong with it, and that the report stops being wrong when the thing is fixed —
    /// a stale badge is worse than no badge, because people trust it.
    /// </para>
    /// </summary>
    public class NodeProblemTests
    {
        private const string Folder = "Assets/__NodeProblemTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__NodeProblemTests");
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

        private static FunctionGraphAsset Predicate(string assetName, params string[] inputs)
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
            return function;
        }

        private static VisualScriptGraphVariable NodeReading(FunctionGraphAsset function, string treeName = "Tree")
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/{treeName}.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            node.SetFunction(function);
            return node;
        }

        // ------------------------------------------------------------------ what a node reports

        [Test]
        public void AnUnfedRequiredPort_IsReportedBeforeAnythingRuns()
        {
            var node = NodeReading(Predicate("IsHurt", "threshold"));

            var problems = NodeProblemCache.For(node);

            Assert.That(problems.Select(problem => problem.Summary),
                Has.Some.Contains("threshold"), "the port that needs feeding has to be named");
            Assert.That(problems.Any(problem => problem.Severity == NodeProblemSeverity.Error), Is.True);
        }

        [Test]
        public void ADriftedContract_IsReportedWithTheRepairNamed()
        {
            var function = Predicate("IsHurt", "threshold");
            var node = NodeReading(function);

            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "boost", label = "boost", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();
            FunctionEvaluator.InvalidateAll();

            var problems = NodeProblemCache.For(node);

            Assert.That(problems.Select(problem => problem.Summary), Has.Some.Contains("boost"));
            Assert.That(problems.Select(problem => problem.Fix), Has.Some.Contains("Refresh"),
                "a badge that says something is wrong without saying what to do is half a feature");
        }

        [Test]
        public void ANodeWithNothingAssigned_IsReported()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Empty.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemCache.For(node), Is.Not.Empty);
        }

        [Test]
        public void ASubTreeNodeWithNoSubTree_IsReported()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Caller.asset");
            var node = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemCache.For(node).Select(problem => problem.Summary),
                Has.Some.Contains("No sub-tree"),
                "the sub-tree node has carried the same invisible-problem gap all along");
        }

        [Test]
        public void AHealthyNode_ReportsNothing()
        {
            var function = Predicate("Constant");
            var node = NodeReading(function);

            Assert.That(NodeProblemCache.For(node), Is.Empty,
                "a badge that appears on a correct node trains people to ignore badges");
        }

        [Test]
        public void ErrorsAreListedBeforeWarnings()
        {
            // Both sources assigned is a warning; the unfed required port is an error.
            var function = Predicate("Ambiguous", "threshold");
            var node = NodeReading(function);
            node.SetScriptGraph(ScriptableObject.CreateInstance<ScriptGraphAsset>());

            NodeProblemCache.Invalidate();

            var problems = NodeProblemCache.For(node);

            Assert.That(problems.Count, Is.GreaterThan(1), "this fixture needs both a warning and an error");
            Assert.That(problems[0].Severity, Is.EqualTo(NodeProblemSeverity.Error),
                "with several problems the one that stops the node running goes first");
        }

        // ------------------------------------------------------------------ freshness

        /// <summary>
        /// The cache exists because a canvas redraws constantly; the risk it introduces is a badge that
        /// outlives the problem. This pins that fixing the problem clears it.
        /// </summary>
        [Test]
        public void RefreshingTheContract_ClearsTheDriftReport()
        {
            var function = Predicate("IsHurt", "threshold");
            var node = NodeReading(function);

            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "boost", label = "boost", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();
            FunctionEvaluator.InvalidateAll();

            Assert.That(NodeProblemCache.For(node).Select(problem => problem.Summary),
                Has.Some.Contains("boost"), "precondition: the drift is being reported");

            node.RefreshParameters();
            NodeProblemCache.Invalidate();

            var after = NodeProblemCache.For(node);

            Assert.That(after.Select(problem => problem.Fix), Has.None.Contains("Refresh"),
                "the drift is repaired, so the badge must stop asking for a refresh");

            // 'boost' has not disappeared, and should not: refreshing turned "this node does not know about
            // boost" into "this node now owes boost a value", which is a different problem with a different
            // fix. That progression is the feature working, not a leftover.
            Assert.That(after.Select(problem => problem.Summary),
                Has.Some.Contains("boost").And.Some.Contains("nothing connected"));
        }

        /// <summary>
        /// The freshness contract in one test: the badge invalidates off the same counter the evaluator does,
        /// so it cannot report a node as fine while evaluation would disagree. Without this, nothing stops
        /// someone caching harder and reintroducing exactly that.
        /// </summary>
        [Test]
        public void EditingAFunction_IsPickedUpWithoutAnExplicitInvalidate()
        {
            var function = Predicate("IsHurt");
            var node = NodeReading(function);

            Assert.That(NodeProblemCache.For(node), Is.Empty, "precondition: nothing wrong yet");

            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "threshold", label = "threshold", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();

            // Only the evaluator is told -- exactly what the import postprocessor does. NodeProblemCache is
            // never touched, and must notice anyway.
            FunctionEvaluator.InvalidateAll();

            Assert.That(NodeProblemCache.For(node), Is.Not.Empty,
                "the badge must go stale on the same signal the evaluator does, or the two can disagree");
        }

        // ------------------------------------------------------------------ extensibility

        /// <summary>
        /// The reason this is a general badge rather than a drift badge: a rule that lives outside the node
        /// has to be able to reach the canvas without the node knowing the rule exists.
        /// </summary>
        [Test]
        public void ARegisteredProvider_CanReportAgainstANodeThatKnowsNothingAboutIt()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Provided.asset");
            var node = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemCache.For(node), Is.Empty, "a Wait node reports nothing about itself");

            System.Func<BehaviorTreeNode, IEnumerable<NodeProblem>> provider = candidate => candidate is WaitTime
                ? new[] { new NodeProblem(NodeProblemSeverity.Warning, "waits are boring") }
                : Enumerable.Empty<NodeProblem>();

            NodeProblemCache.AddProvider(provider);

            try
            {
                Assert.That(NodeProblemCache.For(node).Select(problem => problem.Summary),
                    Has.Some.Contains("waits are boring"));
            }
            finally
            {
                // Providers are static and outlive the test. Leaving one registered would put "waits are
                // boring" on every Wait node in every later test in the run.
                NodeProblemCache.RemoveProvider(provider);
            }

            Assert.That(NodeProblemCache.For(node), Is.Empty, "removing a provider must also take its findings");
        }
    }
}

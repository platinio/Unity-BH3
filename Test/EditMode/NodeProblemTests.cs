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

        /// <summary>
        /// The most common canvas edit there is, and the one the cache used to survive. Every node's base
        /// problem is connection-dependent, so a badge that outlives the connection fixing it is the exact
        /// disagreement this class exists to make unrepresentable.
        ///
        /// <para>
        /// Deliberately no <c>Invalidate</c> call: the point is that feeding the port is enough on its own.
        /// The edit goes through the authoring API rather than a widget, which is also the reason the
        /// invalidation hangs off the graph's element collection instead of the editor call sites — the
        /// collection is what both routes have in common.
        /// </para>
        /// </summary>
        [Test]
        public void ConnectingAPort_ClearsTheBadgeWithoutAnExplicitInvalidate()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Connected.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemCache.For(wait), Is.Not.Empty,
                "precondition: the unfed port is reported, and now cached as reported");

            BehaviorTreeAuthoring.SetValue(tree, wait.Time, 1.5f, -200.0f, 0.0f);

            Assert.That(NodeProblemCache.For(wait), Is.Empty,
                "a badge that survives the connection that fixes it teaches people to ignore badges");
        }

        /// <summary>
        /// The same failure from the other side, and the worse of the two: a node that has been broken since
        /// the cache last looked keeps drawing as healthy, so nothing on the canvas says the tree stopped
        /// working.
        /// </summary>
        [Test]
        public void DisconnectingARequiredPort_BringsTheBadgeBackWithoutAnExplicitInvalidate()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Disconnected.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.SetValue(tree, wait.Time, 1.5f, -200.0f, 0.0f);

            Assert.That(NodeProblemCache.For(wait), Is.Empty,
                "precondition: the fed node is clean, and now cached as clean");

            wait.Time.Disconnect();

            Assert.That(NodeProblemCache.For(wait).Select(problem => problem.Summary),
                Has.Some.Contains("Time"),
                "the node is broken again and must stop drawing as healthy");
        }

        /// <summary>
        /// The one entry nothing could ever drop.
        ///
        /// <para>
        /// Freshness comes from the node's graph raising a change, so a node that has no graph yet has no
        /// signal. Caching its answer would produce the single permanently stale entry in the class — still
        /// reported after the node joins a graph and the problem is fixed. It is computed and returned, but
        /// not stored.
        /// </para>
        /// </summary>
        [Test]
        public void ANodeWithNoGraph_IsAnsweredButNotCached()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/LateJoiner.asset");

            // Defined but not in any graph, which is the state a node is built in before it is added.
            var wait = new WaitTime();
            wait.Define();

            Assert.That(NodeProblemCache.For(wait), Is.Not.Empty,
                "precondition: the unfed port is reported even without a graph");

            tree.graph.Nodes.Add(wait);
            BehaviorTreeAuthoring.SetValue(tree, wait.Time, 1.5f, -200.0f, 0.0f);

            Assert.That(NodeProblemCache.For(wait), Is.Empty,
                "an answer cached before the node had a graph has no signal that can ever drop it, so the "
                + "badge would outlive the fix forever");
        }

        // ------------------------------------------------------------------ unset ports, for every node

        /// <summary>
        /// The universal check, on a node that knows nothing about Functions. <c>WaitTime.Time</c> is one of
        /// the 22 shipped ports that declare no default and throw on first read.
        /// </summary>
        [Test]
        public void AnyNodeWithAnUnfedRequiredPort_IsReported()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Wait.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemCache.For(wait).Select(problem => problem.Summary),
                Has.Some.Contains("Time"),
                "a port that throws on first read is a problem on any node, not just contract-driven ones");
        }

        [Test]
        public void FeedingThePort_ClearsIt()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Fed.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);

            BehaviorTreeAuthoring.SetValue(tree, wait.Time, 1.5f, -200.0f, 0.0f);
            NodeProblemCache.Invalidate();

            Assert.That(NodeProblemCache.For(wait), Is.Empty);
        }

        /// <summary>
        /// A port read through <c>GetComponent</c> never calls <c>GetValue</c>, so unconnected is correct and
        /// must not be reported. Declared at the port rather than inferred from its name.
        /// </summary>
        [Test]
        public void APortDeclaredSafeToLeaveUnconnected_IsNotReported()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Nav.asset");
            var stop = BehaviorTreeAuthoring.AddNode<StopNavAgent>(tree, 0.0f, 0.0f);

            Assert.That(NodeProblemCache.For(stop).Select(problem => problem.Summary),
                Has.None.Contains("Target"),
                "StopNavAgent reads Target through GetComponent and falls back to the agent");
        }

        /// <summary>
        /// The bug the old name-based allowlist had. It excused every port called <c>Target</c>, so a node
        /// that genuinely required one was silently exempt. Whether unconnected is safe depends on how the
        /// node reads the port, which is why the answer now lives on the port.
        /// </summary>
        [Test]
        public void SafetyIsPerPort_NotPerPortName()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/SameName.asset");
            var stop = BehaviorTreeAuthoring.AddNode<StopNavAgent>(tree, 0.0f, 0.0f);
            var face = BehaviorTreeAuthoring.AddNode<FaceTarget>(tree, 200.0f, 0.0f);

            var safe = stop.valueInputs.First(port => port.key == "Target");
            var required = face.valueInputs.First(port => port.key == "TransformTarget");

            Assert.That(safe.IsUnfedRequired, Is.False);
            Assert.That(required.IsUnfedRequired, Is.True,
                "two ports, one of them safe unconnected and one not -- a name-based rule cannot tell them "
                + "apart, and the old one exempted both");
        }

        [Test]
        public void Verify_ReportsAnUnfedRequiredPortAndNamesTheNode()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/VerifyWait.asset");
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(tree, tree.graph.EntryNode, wait, 0);
            BehaviorTreeAuthoring.Save(tree);

            var findings = BehaviorTreeVerification.Verify($"{Folder}/VerifyWait.asset");

            Assert.That(findings, Has.Some.Contains("Time"));
            Assert.That(findings, Has.Some.Contains("unset and will throw"));
        }

        [Test]
        public void Verify_DoesNotReportAPortDeclaredSafeToLeaveUnconnected()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/VerifyNav.asset");
            var stop = BehaviorTreeAuthoring.AddNode<StopNavAgent>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(tree, tree.graph.EntryNode, stop, 0);
            BehaviorTreeAuthoring.Save(tree);

            Assert.That(BehaviorTreeVerification.Verify($"{Folder}/VerifyNav.asset"),
                Has.None.Contains("Target"));
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

            // Fed, so the node is genuinely clean and anything reported below came from the provider.
            BehaviorTreeAuthoring.SetValue(tree, node.Time, 1.0f, -200.0f, 0.0f);
            NodeProblemCache.Invalidate();

            Assert.That(NodeProblemCache.For(node), Is.Empty, "a fed Wait node reports nothing about itself");

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

        /// <summary>
        /// <see cref="RunScriptGraph"/> reports itself, on every instance, configured or not.
        ///
        /// <para>
        /// The node runs the shared script-graph asset outside the machine's variable scope, so graph state
        /// is shared between agents and <c>Self</c> does not resolve to the agent. That is not a
        /// misconfiguration anyone can correct by filling the field in -- the node is wrong when it is fully
        /// set up -- so the warning is unconditional, and it is how the instances already sitting in trees
        /// get found. The class cannot simply be deleted: its name is what assets serialize.
        /// </para>
        /// </summary>
        [Test]
        public void RunScriptGraph_ReportsItselfEvenWhenFullyConfigured()
        {
            var node = new RunScriptGraph();
            node.Define();

            var problems = new List<NodeProblem>();
            node.CollectProblems(problems);

            Assert.IsTrue(problems.Any(problem => problem.Severity == NodeProblemSeverity.Warning
                    && problem.Summary.Contains("no agent context")),
                "A Run Script Graph node did not warn about running the shared asset without agent context:" + "\n"
                + string.Join("\n", problems.Select(problem => $"{problem.Severity}: {problem.Summary}")));

            Assert.IsTrue(problems.Any(problem => problem.Severity == NodeProblemSeverity.Error
                    && problem.Summary.Contains("No script graph assigned")),
                "An unassigned script graph is an error, and the node did not report one.");
        }

    }
}

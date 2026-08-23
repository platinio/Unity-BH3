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
    /// A Function's declared inputs become ports on the Script Graph Variable node that reads it, so a call
    /// site passes arguments instead of relying on the agent happening to declare a variable of the same
    /// name (spec 10, step 2b).
    ///
    /// <para>
    /// These mirror <see cref="SubTreeParameterTests"/> deliberately: the two nodes hold a contract copy for
    /// the same reason and are repaired by the same two verbs, so the coverage should read the same. The
    /// cases that are <em>not</em> in that file are the ones specific to Functions — the ordering trap
    /// below, and the deletion of name matching.
    /// </para>
    /// </summary>
    public class FunctionPortTests
    {
        private const string Folder = "Assets/__FunctionPortTests";

        private GameObject agent;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FunctionPortTests");
            FunctionEvaluator.InvalidateAll();

            agent = new GameObject("Agent");
            agent.AddComponent<Variables>();
        }

        [TearDown]
        public void TearDown()
        {
            if (agent != null) Object.DestroyImmediate(agent);
            AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        /// <summary>
        /// A Function returning one of its declared inputs. <paramref name="returnedInput"/> chooses which,
        /// which is what lets a test prove an argument reached the parameter it was meant for rather than
        /// merely reaching the Function.
        /// </summary>
        private static FunctionGraphAsset Function(
            string assetName, IReadOnlyList<(string key, System.Type type, object defaultValue)> inputs,
            string returnedInput, System.Type resultType)
        {
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = function.graph;

            var input = new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) };
            var output = new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) };
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

            foreach (var (key, type, defaultValue) in inputs)
            {
                var definition = new Unity.VisualScripting.ValueInputDefinition
                {
                    key = key, label = key, type = type
                };

                if (defaultValue != null)
                {
                    definition.hasDefaultValue = true;
                    definition.defaultValue = defaultValue;
                }

                graph.valueInputDefinitions.Add(definition);
            }

            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = resultType
            });

            graph.PortDefinitionsChanged();

            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            if (returnedInput != null)
            {
                input.valueOutputs[returnedInput]
                    .ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);
            }

            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            return function;
        }

        private static FunctionGraphAsset EchoFloat(string assetName, string key, object defaultValue = null)
        {
            return Function(assetName, new[] { (key, typeof(float), defaultValue) }, key, typeof(float));
        }

        private static (BehaviorTreeGraphAsset tree, VisualScriptGraphVariable node) TreeReading(
            FunctionGraphAsset function, string treeName = "Caller")
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/{treeName}.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            node.SetFunction(function);

            return (tree, node);
        }

        // ------------------------------------------------------------------ ports exist and carry defaults

        [Test]
        public void AssigningAFunction_TurnsItsDeclaredInputsIntoPorts()
        {
            var (_, node) = TreeReading(EchoFloat("Echo", "threshold"));

            CollectionAssert.Contains(node.valueInputs.Select(port => port.key).ToList(), "threshold");
        }

        [Test]
        public void AnOptionalInputCarriesItsDefault_AndARequiredOneDoesNot()
        {
            var function = Function("Mixed", new (string, System.Type, object)[]
            {
                ("required", typeof(float), null),
                ("optional", typeof(float), 7.5f)
            }, "optional", typeof(float));

            var (_, node) = TreeReading(function);

            Assert.AreEqual(7.5f, node.valueInputs.First(port => port.key == "optional").GetValue(),
                "an optional input declares a default, so the port is safe to leave unconnected");

            Assert.Throws<MissingValuePortInputException>(
                () => node.valueInputs.First(port => port.key == "required").GetValue(),
                "a required input declares no default, which is what makes an unconnected port reportable");
        }

        [Test]
        public void AFunctionWithNoDeclaredInputs_DeclaresNoPorts()
        {
            var function = Function("Constant", System.Array.Empty<(string, System.Type, object)>(),
                null, typeof(bool));

            // No input to return, so wire Enter straight through and leave Result unfed; this test is about
            // ports, not evaluation.
            var (_, node) = TreeReading(function);

            Assert.That(node.valueInputs.Select(port => port.key), Has.None.EqualTo("threshold"));
        }

        // ------------------------------------------------------------------ arguments arrive from ports

        /// <summary>
        /// The node's half of the bargain: what it hands the evaluator comes from its ports, named and in
        /// port order. Evaluating end to end needs a running machine to supply the agent, so that half is
        /// covered by <c>FunctionBackedScriptGraphVariableTests</c> and by the play-mode demo; this pins the
        /// seam between them.
        /// </summary>
        [Test]
        public void TheArgumentsANodeSupplies_ComeFromItsPorts()
        {
            var (tree, node) = TreeReading(EchoFloat("Echo", "threshold"));

            BehaviorTreeAuthoring.SetValue(tree, node.valueInputs.First(port => port.key == "threshold"),
                42.0f, 0.0f, 0.0f);

            var arguments = (IFunctionArguments)node;

            Assert.AreEqual(1, arguments.Count);
            Assert.AreEqual("threshold", arguments.NameAt(0));
            Assert.AreEqual(42.0f, arguments.ValueAt(0));
        }

        [Test]
        public void AnUnconnectedRequiredPort_FailsNamingTheNodeAndTheInput()
        {
            var (_, node) = TreeReading(EchoFloat("Echo", "threshold"));

            var arguments = (IFunctionArguments)node;

            var exception = Assert.Throws<System.Exception>(() => arguments.ValueAt(0));

            Assert.That(exception.Message, Does.Contain("threshold"),
                "the whole point of the port is that the failure names what is missing and where");

            // Only the genuinely-unconnected case may be relabelled. Asserting the inner exception is what
            // stops the message being pinned onto a fault that came from somewhere else entirely — a
            // connected port evaluates whatever feeds it, which can be another whole graph.
            Assert.That(exception.InnerException, Is.TypeOf<MissingValuePortInputException>());
        }

        /// <summary>
        /// A lifecycle slot cannot declare ports, so a Function needing an argument runs unfed there and
        /// nothing at runtime says so. Verification is the only thing that can.
        ///
        /// <para>
        /// The report used to be blanket — <em>any</em> Function on a lifecycle slot was named — which was
        /// right while those slots could not run a Function at all. They can now, so the finding names the
        /// specific input that can never be supplied, which is the thing an author has to act on. The
        /// picker offers Functions on all four slots, so this is a state reached by choosing, not by
        /// accident.
        /// </para>
        /// </summary>
        [Test]
        public void Verify_ReportsALifecycleFunctionWhoseInputCannotBeSupplied()
        {
            var function = EchoFloat("Echo", "threshold");

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Lifecycle.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptingNode>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(tree, tree.graph.EntryNode, node, 0);

            node.LifecycleGraphs.First().SetFunction(function);
            BehaviorTreeAuthoring.Save(tree);

            var findings = BehaviorTreeVerification.Verify($"{Folder}/Lifecycle.asset");

            Assert.That(findings, Has.Some.Contains("threshold"),
                "the input that can never be supplied is the thing to act on, so it has to be named");
            Assert.That(findings, Has.Some.Contains("Echo"), "the report must name the Function");
        }

        /// <summary>
        /// A Function with no required input is fine on a lifecycle slot — it is run for its effects. The
        /// blanket report this replaces would have named it, which is a false alarm on the shape the picker
        /// now actively offers.
        /// </summary>
        [Test]
        public void Verify_DoesNotReportALifecycleFunctionThatNeedsNothing()
        {
            var function = EchoFloat("Fine", "threshold", defaultValue: 1.0f);

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/LifecycleFine.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptingNode>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(tree, tree.graph.EntryNode, node, 0);

            // OnAwake: run for effects, so any result -- or none -- is right.
            node.LifecycleGraphs.First().SetFunction(function);
            BehaviorTreeAuthoring.Save(tree);

            var findings = BehaviorTreeVerification.Verify($"{Folder}/LifecycleFine.asset");

            Assert.That(findings, Has.None.Contains("Fine"),
                "an input with a default is supplied, so there is nothing to report");
        }

        /// <summary>
        /// <c>OnUpdate</c>'s Function is the node's verdict, so it has to return an <c>ExecutionStatus</c>.
        /// Nothing else in the tree can catch this: the slot has no wire whose type would refuse it.
        /// </summary>
        [Test]
        public void Verify_ReportsAnOnUpdateFunctionThatReturnsTheWrongType()
        {
            var function = EchoFloat("NotAVerdict", "threshold", defaultValue: 1.0f);

            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/LifecycleUpdate.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptingNode>(tree, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Connect(tree, tree.graph.EntryNode, node, 0);

            // Slots are in declaration order: OnAwake, OnEnter, OnUpdate, OnExit.
            node.GraphSlots[2].SetFunction(function);
            BehaviorTreeAuthoring.Save(tree);

            var findings = BehaviorTreeVerification.Verify($"{Folder}/LifecycleUpdate.asset");

            Assert.That(findings, Has.Some.Contains("NotAVerdict"), "the report must name the Function");
            Assert.That(findings, Has.Some.Contains("ExecutionStatus"),
                "and what the slot required, or it does not say what to change");
        }

        /// <summary>
        /// The point of step 2b: an agent variable that happens to share a declared input's name must no
        /// longer feed it. Before ports, this was the <em>only</em> way to supply an argument, and it was
        /// invisible on the node.
        /// </summary>
        [Test]
        public void AnAgentVariableSharingAnInputName_NoLongerFeedsIt()
        {
            var function = EchoFloat("Echo", "threshold", defaultValue: 1.0f);
            var (_, node) = TreeReading(function);

            // The coincidence that used to be the only way to supply an argument.
            agent.GetComponent<Variables>().declarations.Set("threshold", 99.0f);

            var variable = new ScriptGraphVariable();
            variable.SetFunction(function);

            Assert.AreEqual(1.0f, variable.GetValue<float>(agent, (IFunctionArguments)node),
                "the value must come from the node's port, which carries the Function's declared default. "
                + "Name matching against the agent scope was deleted, not merely deprioritised.");
        }

        /// <summary>
        /// An input nothing stages has no value at all — the graph reads it as a missing key, which is the
        /// <c>KeyNotFoundException</c> spec 10 cites as the failure step 2b removes. It is refused before the
        /// graph runs instead, because the bare exception names the key and nothing else and so points at
        /// the Function when the thing to fix is the call site.
        /// </summary>
        [Test]
        public void AnInputWithNoArgument_IsRefusedBeforeTheGraphRuns()
        {
            var variable = new ScriptGraphVariable();
            variable.SetFunction(EchoFloat("Unstaged", "threshold", defaultValue: 1.0f));

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => variable.GetValue<float>(agent, (IFunctionArguments)null));

            Assert.That(exception.Message, Does.Contain("threshold"), "name the input that is owed");
            Assert.That(exception.Message, Does.Contain("Unstaged"), "name the Function");
            Assert.That(exception.Message, Does.Contain("fn_refresh_ports"), "name the repair");
        }

        /// <summary>
        /// The same refusal reached through a real node, and the message must blame the node rather than the
        /// Function — this is the drift shape a caller hits when its contract copy has fallen behind.
        /// </summary>
        [Test]
        public void AnInputTheCallerHasNoPortFor_IsRefusedNamingTheCallSite()
        {
            var function = EchoFloat("Echo", "threshold", defaultValue: 1.0f);
            var (_, node) = TreeReading(function);

            // Give the Function a second input and leave the node un-refreshed, so it has no port for it.
            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "boost", label = "boost", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();
            FunctionEvaluator.InvalidateAll();

            var variable = new ScriptGraphVariable();
            variable.SetFunction(function);

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => variable.GetValue<float>(agent, (IFunctionArguments)node));

            Assert.That(exception.Message, Does.Contain("boost"));
            Assert.That(exception.Message, Does.Contain(node.NodeName),
                "the call site owes the argument, so the call site is what the message must name");
        }

        /// <summary>
        /// The trap that makes positional staging unsafe, pinned so nobody 'simplifies' the index map away.
        ///
        /// <para>
        /// <c>FunctionBindingPlan.Resolve</c> skips any declared input with no live port on the graph's
        /// input unit, while the node's contract copy lists every declared input. Here the <em>first</em>
        /// declared input is the skipped one, so the node's port 1 must bind to plan input 0. Staging
        /// position-for-position would send 'wanted' into the wrong parameter and return the wrong number,
        /// with no error anywhere.
        /// </para>
        /// </summary>
        [Test]
        public void AStaleLeadingPort_DoesNotShiftTheArgumentsBehindIt()
        {
            var function = Function("Shifted", new (string, System.Type, object)[]
            {
                ("ignored", typeof(float), 1.0f),
                ("wanted", typeof(float), 2.0f)
            }, "wanted", typeof(float));

            var (tree, node) = TreeReading(function);

            // Drop the *first* input from the Function without refreshing the node, so the node keeps two
            // ports while the plan resolves only one. This is ordinary drift, and it is what makes an
            // index-for-index stage unsafe: node port 0 would be staged into plan input 0, sending
            // 'ignored' into 'wanted'.
            function.graph.valueInputDefinitions.Clear();
            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "wanted", label = "wanted", type = typeof(float), hasDefaultValue = true, defaultValue = 2.0f
            });
            function.graph.PortDefinitionsChanged();

            var input = function.graph.units.OfType<ScriptGraphInput>().First();
            var output = function.graph.units.OfType<ScriptGraphOutput>().First();
            input.valueOutputs["wanted"].ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);

            FunctionEvaluator.InvalidateAll();

            Assert.That(node.Parameters.Count, Is.EqualTo(2), "the node has deliberately not been refreshed");

            BehaviorTreeAuthoring.SetValue(tree, node.valueInputs.First(port => port.key == "wanted"),
                5.0f, 0.0f, 0.0f);

            // Stage through the same seam the node uses, in the node's own (stale) port order.
            var variable = new ScriptGraphVariable();
            variable.SetFunction(function);

            Assert.AreEqual(5.0f, variable.GetValue<float>(agent, (IFunctionArguments)node),
                "'wanted' was staged into the wrong input — the argument map must resolve by name, because "
                + "the caller's contract copy and the binding plan can disagree in length and order");
        }

        // ------------------------------------------------------------------ drift, refresh, and what it costs

        [Test]
        public void ChangingTheFunctionsContract_IsReportedAsDrift_AndRepairedByRefresh()
        {
            var function = EchoFloat("Echo", "threshold");
            var (_, node) = TreeReading(function);

            Assert.That(node.DescribeContractDrift(), Is.Empty, "freshly assigned, so nothing has drifted");

            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "boost", label = "boost", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();
            FunctionEvaluator.InvalidateAll();

            Assert.That(node.DescribeContractDrift(), Has.Some.Contains("boost"),
                "a caller must report a contract it no longer matches");

            node.RefreshParameters();

            Assert.That(node.DescribeContractDrift(), Is.Empty);
            CollectionAssert.Contains(node.valueInputs.Select(port => port.key).ToList(), "boost");
        }

        /// <summary>
        /// The tool owner's call on the spec's open question: a refresh still drops the connection, but it
        /// says which one and what was feeding it.
        /// </summary>
        [Test]
        public void ARefreshThatRemovesAConnectedPort_NamesWhatItDropped()
        {
            var function = EchoFloat("Echo", "threshold");
            var (tree, node) = TreeReading(function);

            // A value node is pulled through a port, never parented — so it is added to the graph and wired
            // to the port, with no transition.
            var literal = BehaviorTreeAuthoring.AddNode<FloatLiteral>(tree, -200.0f, 0.0f);
            node.valueInputs.First(port => port.key == "threshold")
                .ConnectToValid(literal.valueOutputs.First());

            function.graph.valueInputDefinitions.Clear();
            function.graph.PortDefinitionsChanged();
            FunctionEvaluator.InvalidateAll();

            var dropped = node.RefreshParameters();

            Assert.That(dropped, Has.Some.Contains("threshold"),
                "removing a connected port must name the input that went");
        }

        [Test]
        public void ARefreshThatRemovesAnUnconnectedPort_ReportsNothing()
        {
            var function = EchoFloat("Echo", "threshold");
            var (_, node) = TreeReading(function);

            function.graph.valueInputDefinitions.Clear();
            function.graph.PortDefinitionsChanged();
            FunctionEvaluator.InvalidateAll();

            Assert.That(node.RefreshParameters(), Is.Empty,
                "nothing was feeding it, so nothing was lost and there is nothing to say");
        }

        // ------------------------------------------------------------------ the copy survives a round trip

        /// <summary>
        /// The reason the contract is copied at all: <c>Definition()</c> runs during deserialization and
        /// drops connections to port keys that do not exist yet, so ports must come from the node's own
        /// serialized data rather than from the Function asset.
        /// </summary>
        [Test]
        public void PortsSurviveTheReloadTheMachineDoes()
        {
            var function = EchoFloat("Echo", "threshold");
            var (tree, node) = TreeReading(function);

            BehaviorTreeAuthoring.SetValue(tree, node.valueInputs.First(port => port.key == "threshold"),
                8.0f, 0.0f, 0.0f);
            BehaviorTreeAuthoring.Save(tree);

            var reloaded = BehaviorTreeVerification.Reload($"{Folder}/Caller.asset");
            var reloadedNode = reloaded.graph.Nodes.OfType<VisualScriptGraphVariable>().First();

            CollectionAssert.Contains(reloadedNode.valueInputs.Select(port => port.key).ToList(), "threshold");
            Assert.AreEqual(8.0f, reloadedNode.valueInputs.First(port => port.key == "threshold").GetValue());
        }

        // ------------------------------------------------------------------ verification

        [Test]
        public void Verify_ReportsANodeWhoseContractCopyHasDrifted()
        {
            var function = EchoFloat("Echo", "threshold");
            var (tree, _) = TreeReading(function);
            BehaviorTreeAuthoring.Save(tree);

            function.graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
            {
                key = "boost", label = "boost", type = typeof(float)
            });
            function.graph.PortDefinitionsChanged();
            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();
            FunctionEvaluator.InvalidateAll();

            var findings = BehaviorTreeVerification.Verify($"{Folder}/Caller.asset");

            Assert.That(findings, Has.Some.Contains("Function contract"),
                "drift has to reach bt_verify, or the only person who learns about it is whoever happens "
                + "to right-click the node");
            Assert.That(findings, Has.Some.Contains("fn_refresh_ports"), "the report must name the repair");
        }

        // ------------------------------------------------------------------ sizing

        [Test]
        public void ResizingToFitPorts_GrowsTheNodeBeyondTheDefault()
        {
            var function = Function("Wide", new (string, System.Type, object)[]
            {
                ("aVeryLongParameterNameIndeed", typeof(float), 0.0f),
                ("second", typeof(float), 0.0f),
                ("third", typeof(float), 0.0f)
            }, "second", typeof(float));

            var (_, node) = TreeReading(function);

            var resized = ContractPortLayout.ResizeToFitPorts(node);

            Assert.Greater(resized.width, 150.0f, "a long port label has to widen the node or it draws clipped");
            Assert.Greater(resized.height, 100.0f, "three ports do not fit in the default height");
            Assert.AreEqual(resized, node.Position, "the computed size must actually be written to the node");
        }

        /// <summary>
        /// The gap between port rows has to be counted, not just the rows. The widget draws
        /// <c>spaceBetweenPorts</c> between every pair, and the height that used to be computed for them
        /// subtracted one such gap it had never added — so a node's ports drew past its own bottom edge, by
        /// more the more ports it had.
        /// </summary>
        [Test]
        public void EachExtraPort_CostsARowAndTheGapBeforeIt()
        {
            // Both sizes clear the 100px minimum a node is never drawn below. Comparing against a one-port
            // node would measure the floor rather than the growth, which is what the first version of this
            // test did.
            var (_, three) = TreeReading(Function("Three", new (string, System.Type, object)[]
            {
                ("a", typeof(float), 0.0f), ("b", typeof(float), 0.0f), ("c", typeof(float), 0.0f)
            }, "a", typeof(float)));

            var (_, five) = TreeReading(Function("Five", new (string, System.Type, object)[]
            {
                ("a", typeof(float), 0.0f), ("b", typeof(float), 0.0f), ("c", typeof(float), 0.0f),
                ("d", typeof(float), 0.0f), ("e", typeof(float), 0.0f)
            }, "a", typeof(float)));

            var grew = ContractPortLayout.ResizeToFitPorts(five).height
                       - ContractPortLayout.ResizeToFitPorts(three).height;

            var expected = 2.0f * (UnityEditor.EditorGUIUtility.singleLineHeight
                                   + BehaviorTreeNodeElementWidget.Styles.spaceBetweenPorts);

            Assert.AreEqual(expected, grew, 0.01f,
                "two extra ports cost two rows and the two gaps drawn before them");
        }

        /// <summary>
        /// Sizing a node outside the draw path has to reach the same number the widget reserves when it draws
        /// one, so the header allowance is read from the widget rather than copied. It was a bare literal in
        /// both places, with a comment here saying the widget's could not be referenced.
        /// </summary>
        [Test]
        public void TheHeaderAllowance_IsTheWidgetsOwnNumber()
        {
            // Three ports rather than one, so the result is the computed height and not the 100px floor.
            var (_, node) = TreeReading(Function("Three", new (string, System.Type, object)[]
            {
                ("a", typeof(float), 0.0f), ("b", typeof(float), 0.0f), ("c", typeof(float), 0.0f)
            }, "a", typeof(float)));

            var height = ContractPortLayout.ResizeToFitPorts(node).height;

            var rows = (3.0f * UnityEditor.EditorGUIUtility.singleLineHeight)
                       + (2.0f * BehaviorTreeNodeElementWidget.Styles.spaceBetweenPorts);

            Assert.AreEqual(
                BehaviorTreeNodeElementWidget.HEADER_AND_FOOTER_HEIGHT + rows,
                height,
                0.01f,
                "a sized node is the widget's own header allowance plus its rows");
        }
    }
}

using System;
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
    /// A Script Graph Variable's Output is typed from the Function it reads, so the connection gate can refuse
    /// a wire the Function's result could never fill (spec 10, step 2d).
    ///
    /// <para>
    /// Step 2b typed the inputs from the contract copy and left the output hard-coded to <c>object</c> -- which
    /// converts to nearly anything, so a Function returning <c>bool</c> could be wired into a Transform port
    /// and fail at the first tick. The node is still generic until a Function is assigned, because nothing is
    /// known yet; the moment one is, the port takes its type.
    /// </para>
    /// </summary>
    public class FunctionOutputTypeTests
    {
        private const string Folder = "Assets/__FunctionOutputTypeTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FunctionOutputTypeTests");
            FunctionEvaluator.InvalidateAll();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        // ------------------------------------------------------------------ fixtures

        private static FunctionGraphAsset Function(string assetName, Type resultType)
        {
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = function.graph;

            graph.units.Add(new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) });
            graph.units.Add(new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) });
            graph.controlInputDefinitions.Add(new ControlInputDefinition { key = FunctionGraphAsset.EnterKey, label = FunctionGraphAsset.EnterKey });
            graph.controlOutputDefinitions.Add(new ControlOutputDefinition { key = FunctionGraphAsset.ExitKey, label = FunctionGraphAsset.ExitKey });

            if (resultType != null)
            {
                graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
                {
                    key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = resultType
                });
            }

            graph.PortDefinitionsChanged();
            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            return function;
        }

        /// <summary>Changes what an existing Function declares it returns, the way an author editing it would.</summary>
        private static void Retype(FunctionGraphAsset function, Type resultType)
        {
            var graph = function.graph;
            graph.valueOutputDefinitions.Clear();
            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = resultType
            });
            graph.PortDefinitionsChanged();
        }

        private static (BehaviorTreeGraphAsset tree, VisualScriptGraphVariable node, FaceTarget sink) TreeWithSink(
            string name = "Tree")
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/{name}.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);

            // FaceTarget declares a Transform port, a GameObject port and two float ports on one node, which
            // is every destination shape these tests need without inventing a node type.
            var sink = BehaviorTreeAuthoring.AddNode<FaceTarget>(tree, 300.0f, 0.0f);

            return (tree, node, sink);
        }

        // ------------------------------------------------------------------ the port takes the Function's type

        [Test]
        public void ANodeWithNoFunction_IsGeneric()
        {
            var (_, node, _) = TreeWithSink();

            Assert.AreEqual(typeof(object), node.Output.Type,
                "nothing is known until a Function is chosen, so the port stays generic rather than guessing");
        }

        [Test]
        public void AssigningAFunction_TypesTheOutputImmediately()
        {
            var (_, node, _) = TreeWithSink();

            node.SetFunction(Function("IsHurt", typeof(bool)));

            Assert.AreEqual(typeof(bool), node.Output.Type);
            Assert.AreEqual(typeof(bool), node.OutputType);
        }

        [Test]
        public void ClearingTheFunction_ReturnsTheOutputToGeneric()
        {
            var (_, node, _) = TreeWithSink();
            node.SetFunction(Function("IsHurt", typeof(bool)));

            node.SetFunction(null);

            Assert.AreEqual(typeof(object), node.Output.Type);
        }

        [Test]
        public void AFunctionWithNoResult_LeavesTheOutputGeneric()
        {
            var (_, node, _) = TreeWithSink();

            node.SetFunction(Function("Unfinished", null));

            Assert.AreEqual(typeof(object), node.Output.Type,
                "no result declared means no type to take; bt_verify reports the missing Result separately");
        }

        // ------------------------------------------------------------------ the gate refuses what cannot fit

        [Test]
        public void AGenericOutput_MayFeedAnything_WhichIsTheProblem()
        {
            var (_, node, sink) = TreeWithSink();

            Assert.IsTrue(sink.TransformTarget.CanConnectToValid(node.Output));
            Assert.IsTrue(sink.RotationSpeed.CanConnectToValid(node.Output));
        }

        [Test]
        public void ABooleanOutput_CannotBeWiredToATransformPort()
        {
            var (_, node, sink) = TreeWithSink();
            node.SetFunction(Function("IsHurt", typeof(bool)));

            Assert.IsFalse(sink.TransformTarget.CanConnectToValid(node.Output),
                "this is the whole point: the wire is refused when drawn, not discovered at the first tick");
            Assert.IsFalse(sink.Target.CanConnectToValid(node.Output));

            Assert.Throws<InvalidConnectionException>(() => node.Output.ValidlyConnectTo(sink.TransformTarget));
        }

        [Test]
        public void ATypedOutput_StillFeedsWhatItConvertsTo()
        {
            var (_, node, sink) = TreeWithSink();
            node.SetFunction(Function("ReadHp", typeof(float)));

            Assert.IsTrue(sink.RotationSpeed.CanConnectToValid(node.Output));
            Assert.DoesNotThrow(() => node.Output.ValidlyConnectTo(sink.RotationSpeed));
        }

        // ------------------------------------------------------------------ retyping an already-wired node

        [Test]
        public void RetypingAWiredOutput_DemotesTheWireItCanNoLongerFeed_ToAnInvalidConnection()
        {
            var (tree, node, sink) = TreeWithSink();

            // Generic, so this is allowed -- which is exactly the hole.
            node.Output.ValidlyConnectTo(sink.TransformTarget);
            Assert.IsTrue(node.Output.validConnections.Any());

            var lost = node.SetFunction(Function("IsHurt", typeof(bool)));

            Assert.IsFalse(node.Output.validConnections.Any(),
                "a bool cannot feed a Transform port, so the wire must stop being a valid connection");
            Assert.IsTrue(tree.graph.invalidConnections.Any(c => c.destination == sink.TransformTarget),
                "and it is demoted rather than deleted, so the author can see which wire stopped fitting");

            Assert.That(lost, Has.Some.Contains("TransformTarget").And.Some.Contains("Boolean"),
                "what the retype cost is reported by name");
        }

        [Test]
        public void RetypingAWiredOutput_KeepsTheWireThatStillFits()
        {
            var (tree, node, sink) = TreeWithSink();

            node.Output.ValidlyConnectTo(sink.RotationSpeed);

            var lost = node.SetFunction(Function("ReadHp", typeof(float)));

            Assert.IsTrue(node.Output.validConnections.Any(c => c.destination == sink.RotationSpeed));
            Assert.IsFalse(tree.graph.invalidConnections.Any());
            Assert.That(lost, Has.None.Contains("RotationSpeed"));
        }

        [Test]
        public void ReassigningToAFittingFunction_RevalidatesTheDemotedWire()
        {
            var (tree, node, sink) = TreeWithSink();

            node.Output.ValidlyConnectTo(sink.RotationSpeed);
            node.SetFunction(Function("IsHurt", typeof(bool)));
            Assume.That(tree.graph.invalidConnections.Any(), "bool cannot feed a float port");

            node.SetFunction(Function("ReadHp", typeof(float)));

            Assert.IsTrue(node.Output.validConnections.Any(c => c.destination == sink.RotationSpeed),
                "a demoted wire is preserved, so choosing a Function that fits brings it back without rewiring");
            Assert.IsFalse(tree.graph.invalidConnections.Any());
        }

        // ------------------------------------------------------------------ the type is remembered, not read live

        [Test]
        public void AFunctionThatChangesItsResultAfterAssignment_IsReportedAsDrift_NotSilentlyRetyped()
        {
            var (_, node, _) = TreeWithSink();
            var function = Function("Mutable", typeof(bool));
            node.SetFunction(function);

            Retype(function, typeof(float));

            Assert.AreEqual(typeof(bool), node.Output.Type,
                "the port keeps the remembered type until refreshed -- the same contract-copy rule as inputs");
            Assert.That(node.DescribeContractDrift(), Has.Some.Contains("Result").And.Some.Contains("Single"));

            node.RefreshParameters();

            Assert.AreEqual(typeof(float), node.Output.Type);
            Assert.That(node.DescribeContractDrift(), Is.Empty);
        }

        [Test]
        public void TheOutputType_SurvivesSerialization()
        {
            var (tree, node, _) = TreeWithSink("RoundTrip");
            node.SetFunction(Function("IsHurt", typeof(bool)));

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            var path = AssetDatabase.GetAssetPath(tree);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var reloaded = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
            var reloadedNode = reloaded.graph.Nodes.OfType<VisualScriptGraphVariable>().Single();

            Assert.AreEqual(typeof(bool), reloadedNode.Output.Type,
                "read from the serialized copy, so it is right even on a load where the asset has not resolved");
        }

        // ------------------------------------------------------------------ bt_verify sees it too

        [Test]
        public void BtVerify_NamesAnInvalidConnection_WithBothPortTypes()
        {
            var (tree, node, sink) = TreeWithSink("Verified");
            node.Output.ValidlyConnectTo(sink.TransformTarget);
            node.SetFunction(Function("IsHurt", typeof(bool)));

            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();

            var findings = BehaviorTreeVerification.Verify(AssetDatabase.GetAssetPath(tree));

            Assert.That(findings, Has.Some.Contains("invalid connection")
                .And.Some.Contains("Boolean")
                .And.Some.Contains("Transform"),
                "the canvas draws it red; the CLI has to be able to say the same thing");
        }
    }
}

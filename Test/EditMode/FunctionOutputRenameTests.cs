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
    /// Renaming a Function's declared output carries its wires along, so the one-click repair for a
    /// misnamed result leaves a Function that both reads and runs.
    ///
    /// <para>
    /// The case behind this: a designer names their Function's output something descriptive —
    /// <c>SelectedPosition</c> — not knowing callers read the output named <c>Result</c> specifically. The
    /// Function evaluates fine and is offered nowhere. The repair has to move the wire too, because a
    /// definition rename alone rebuilds the graph-output unit's ports and strands the connection on a port
    /// that no longer exists.
    /// </para>
    /// </summary>
    public class FunctionOutputRenameTests
    {
        private const string Folder = "Assets/__FunctionOutputRenameTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FunctionOutputRenameTests");
            FunctionEvaluator.InvalidateAll();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        // ------------------------------------------------------------------ fixture

        /// <summary>
        /// A runnable Function whose one output is named <paramref name="outputKey"/>, fed by a literal —
        /// the shape a designer authors by hand, wire included.
        /// </summary>
        private static FunctionGraphAsset FedFunction(string assetName, string outputKey, Type type)
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
            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = outputKey, label = outputKey, type = type
            });

            graph.PortDefinitionsChanged();
            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            var literal = new Unity.VisualScripting.Literal(typeof(Vector3), Vector3.one)
            {
                position = new Vector2(0.0f, 160.0f)
            };
            graph.units.Add(literal);
            literal.output.ValidlyConnectTo(output.valueInputs[outputKey]);

            AssetDatabase.CreateAsset(function, $"{Folder}/{assetName}.asset");
            return function;
        }

        // ------------------------------------------------------------------ the repair

        [Test]
        public void RenamingTheOutput_MakesTheFunctionReadable_AndKeepsItsWire()
        {
            var function = FedFunction("PickSpot", "SelectedPosition", typeof(Vector3));

            Assert.IsNull(function.ResultType, "misnamed means unreadable — that is the bug being repaired");

            FunctionGraphAuthoring.RenameOutput(function, "SelectedPosition", FunctionGraphAsset.ResultKey);

            Assert.AreEqual(typeof(Vector3), function.ResultType);

            Assert.IsTrue(
                function.graph.valueConnections.Any(connection =>
                    connection.destination.key == FunctionGraphAsset.ResultKey
                    && connection.source.unit is Unity.VisualScripting.Literal),
                "a rename that drops the wire trades a Function nobody can pick for one that returns "
                + "nothing at runtime — strictly worse, because now it fails after being chosen");

            Assert.IsFalse(
                function.graph.valueConnections.Any(c => c.destination.key == "SelectedPosition"),
                "no connection may keep pointing at the port the rename removed");

            Assert.IsTrue(FunctionBindingPlan.Resolve(function).IsUsable);
        }

        [Test]
        public void RenamingAnOutputTheFunctionDoesNotDeclare_Refuses()
        {
            var function = FedFunction("PickSpot", "SelectedPosition", typeof(Vector3));

            Assert.Throws<ArgumentException>(() =>
                FunctionGraphAuthoring.RenameOutput(function, "Elsewhere", FunctionGraphAsset.ResultKey));
        }

        [Test]
        public void RenamingOntoANameAlreadyDeclared_Refuses()
        {
            var function = FedFunction("PickSpot", "SelectedPosition", typeof(Vector3));

            function.graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey, label = FunctionGraphAsset.ResultKey, type = typeof(Vector3)
            });
            function.graph.PortDefinitionsChanged();

            Assert.Throws<ArgumentException>(
                () => FunctionGraphAuthoring.RenameOutput(function, "SelectedPosition", FunctionGraphAsset.ResultKey),
                "two outputs under one key is a graph no author can tell apart, and silently merging them "
                + "would decide which one wins on their behalf");
        }
    }
}

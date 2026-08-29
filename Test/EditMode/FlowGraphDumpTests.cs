using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Covers <see cref="FlowGraphDump"/>, and pins the Visual Scripting authoring API alongside it. Every
    /// graph here is assembled in code, so these also answer whether script graphs can be generated rather
    /// than only drawn by hand.
    /// </summary>
    [TestFixture]
    public class FlowGraphDumpTests
    {
        private static T AddUnit<T>(FlowGraph graph, float x, float y) where T : class, IUnit, new()
        {
            var unit = new T { position = new Vector2(x, y) };
            graph.units.Add(unit);

            return unit;
        }

        [Test]
        public void UnitsAddedInCodeAreDefined()
        {
            var graph = new FlowGraph();
            var branch = AddUnit<If>(graph, 0.0f, 0.0f);

            Assert.IsTrue(branch.isDefined, "Adding a unit to a flow graph must define its ports.");
            StringAssert.Contains("\"unit\": \"If\"", FlowGraphDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void InlinePortValuesAreShown()
        {
            var graph = new FlowGraph();
            var branch = AddUnit<If>(graph, 0.0f, 0.0f);

            branch.condition.SetDefaultValue(true);

            StringAssert.Contains("\"condition\": \"True\"", FlowGraphDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void ValueConnectionsReportTheirSource()
        {
            var graph = new FlowGraph();
            var branch = AddUnit<If>(graph, 0.0f, 0.0f);

            // BH3 has its own Literal node, and this file's namespace makes that one win, so qualify
            var literal = new Unity.VisualScripting.Literal(typeof(bool), true) { position = new Vector2(-200.0f, 0.0f) };
            graph.units.Add(literal);

            literal.output.ValidlyConnectTo(branch.condition);

            StringAssert.Contains("\"condition\": \"<- Literal.output\"", FlowGraphDump.ToJson(graph, "Fixture"),
                "A wired port must name the unit feeding it.");
        }

        [Test]
        public void ControlFlowEdgesAreReported()
        {
            var graph = new FlowGraph();
            var first = AddUnit<If>(graph, 0.0f, 0.0f);
            var second = AddUnit<If>(graph, 200.0f, 0.0f);

            first.ifTrue.ValidlyConnectTo(second.enter);

            StringAssert.Contains("-> If.enter", FlowGraphDump.ToJson(graph, "Fixture"),
                "Control edges are what determine execution order, so they must be visible.");
        }

        [Test]
        public void GraphPortDefinitionsAreReportedWithTheirTypes()
        {
            // the signature a runnable graph is called through: get the key or the type wrong and the graph
            // returns nothing, which is invisible in the unit list
            var graph = new FlowGraph();
            graph.controlInputDefinitions.Add(new ControlInputDefinition { key = "Enter", label = "Enter" });
            // BH3 has its own ValueOutputDefinition, and this file's namespace makes that one win
            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = "Result", label = "Result", type = typeof(float)
            });

            var json = FlowGraphDump.ToJson(graph, "Fixture");

            StringAssert.Contains("\"controlIn\": \"Enter\"", json);
            StringAssert.Contains("\"valueOut\": \"Result : Single\"", json);
        }

        [Test]
        public void GraphWithoutPortDefinitionsOmitsThePortsSection()
        {
            // a plain machine graph declares no ports, and should not grow an empty section because of it
            var graph = new FlowGraph();
            AddUnit<If>(graph, 0.0f, 0.0f);

            StringAssert.DoesNotContain("\"ports\"", FlowGraphDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void NullGraphIsReportedRatherThanThrowing()
        {
            // an unassigned Script Graph node is a normal authoring state, not an error
            StringAssert.Contains("graph is null", FlowGraphDump.ToJson(null, "Fixture"));
        }
    }
}

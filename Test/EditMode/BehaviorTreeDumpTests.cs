using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Covers <see cref="BehaviorTreeDump"/>, and in doing so pins the programmatic authoring API it reads
    /// from. Every tree here is assembled in code the same way an editor script would assemble one, so if
    /// adding a node or a parent/child link ever stops working these fail first.
    /// </summary>
    [TestFixture]
    public class BehaviorTreeDumpTests
    {
        [Test]
        public void TransitionPlaceholdersDoNotBreakTheDump()
        {
            // Every transition created in the editor owns an invisible PlaceHolderNode so the transition
            // line can be selected. It has no ports and is never defined, and reading them used to throw
            // and abort the whole asset — so any hand-edited tree dumped nothing at all.
            var graph = new BehaviorTreeGraph();
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(graph, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, sequence);

            var transition = graph.Transitions.First();
            var placeholder = new PlaceHolderNode(transition);
            graph.Nodes.Add(placeholder);

            string json = null;
            Assert.DoesNotThrow(() => json = BehaviorTreeDump.ToJson(graph, "Fixture"),
                "An invisible transition placeholder must not take the report down with it.");

            StringAssert.DoesNotContain("PlaceHolderNode", json,
                "Canvas plumbing is not authored structure, so it should not appear as an orphan.");
            StringAssert.Contains("Sequence", json, "The rest of the tree still has to come out.");
        }

        [Test]
        public void NodesReportTheirGuid()
        {
            // the guid is how a later edit addresses a node it did not create — name and type are not
            // unique, and position moves the moment someone tidies the canvas
            var graph = new BehaviorTreeGraph();
            var cooldown = BehaviorTreeAuthoring.AddNode<Cooldown>(graph, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, cooldown);

            StringAssert.Contains("\"guid\": \"" + cooldown.guid + "\"", BehaviorTreeDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void GuardsReportTheirGuid()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(graph, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, sequence);

            var guard = BehaviorTreeAuthoring.AddNode<BooleanConditionalExecution>(graph, -150.0f, 100.0f);
            guard.UpdateOwner(sequence);

            StringAssert.Contains("\"guid\": \"" + guard.guid + "\"", BehaviorTreeDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void AConstructedGraphHasExactlyOneEntryNode()
        {
            var graph = new BehaviorTreeGraph();

            Assert.AreEqual(1, graph.Nodes.OfType<Entry>().Count(),
                "A second Entry would render on the canvas and compete with graph.EntryNode.");
        }

        [Test]
        public void CreateEmptyHasExactlyOneEntryNode()
        {
            var graph = BehaviorTreeGraph.CreateEmpty();

            Assert.AreEqual(1, graph.Nodes.OfType<Entry>().Count(),
                "CreateEmpty is what a new graph asset is built from, so a duplicate Entry ships to every new tree.");
        }

        [Test]
        public void NodesAddedInCodeAreDefinedAndReachable()
        {
            var graph = new BehaviorTreeGraph();
            var cooldown = BehaviorTreeAuthoring.AddNode<Cooldown>(graph, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, cooldown);

            Assert.IsTrue(cooldown.isDefined, "Adding a node to a graph must define its ports, as AfterAdd does in the editor.");
            Assert.IsNotNull(cooldown.Duration, "A defined Cooldown exposes its Duration port.");

            StringAssert.Contains("Cooldown", BehaviorTreeDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void ChildrenAreReportedInCanvasOrderNotInsertionOrder()
        {
            var graph = new BehaviorTreeGraph();
            var selector = BehaviorTreeAuthoring.AddNode<Selector>(graph, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, selector);

            // added right first, so insertion order and canvas order disagree. Both indices are left at 0,
            // the shape every tree authored before explicit priorities has, so canvas X decides.
            var right = BehaviorTreeAuthoring.AddNode<Repeater>(graph, 200.0f, 200.0f);
            var left = BehaviorTreeAuthoring.AddNode<WaitTime>(graph, -200.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(graph, selector, right, 0);
            BehaviorTreeAuthoring.Connect(graph, selector, left, 0);

            string json = BehaviorTreeDump.ToJson(graph, "Fixture");

            Assert.Less(json.IndexOf("\"Wait\""), json.IndexOf("\"Repeater\""),
                "The runtime sorts children by canvas X, so the dump has to agree or it misreports priority.");
        }

        [Test]
        public void InlinePortValuesAreShown()
        {
            var graph = new BehaviorTreeGraph();
            var cooldown = BehaviorTreeAuthoring.AddNode<Cooldown>(graph, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, cooldown);

            cooldown.Duration.SetDefaultValue(7.5f);

            StringAssert.Contains("\"Duration\": \"7.5\"", BehaviorTreeDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void WiredPortsShowWhereTheValueComesFrom()
        {
            var graph = new BehaviorTreeGraph();
            var cooldown = BehaviorTreeAuthoring.AddNode<Cooldown>(graph, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, cooldown);

            var literal = BehaviorTreeAuthoring.AddNode<FloatLiteral>(graph, -200.0f, 100.0f);
            literal.Value.ValidlyConnectTo(cooldown.Duration);

            StringAssert.Contains("\"Duration\": \"<- Float Literal.Value\"", BehaviorTreeDump.ToJson(graph, "Fixture"),
                "A connected port must name its source rather than report an inline value.");
        }

        [Test]
        public void NodesNothingReachesAreCalledOut()
        {
            var graph = new BehaviorTreeGraph();
            BehaviorTreeAuthoring.AddNode<Sequence>(graph, 300.0f, 300.0f);

            StringAssert.Contains("unreachable", BehaviorTreeDump.ToJson(graph, "Fixture"),
                "A node left unconnected is the most common authoring mistake, so it must be visible.");
        }
    }
}

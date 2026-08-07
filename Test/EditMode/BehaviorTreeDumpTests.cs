using System.Linq;
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
        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child, int index = 0)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, index);
            graph.Transitions.Add(transition);
        }

        private static T AddNode<T>(BehaviorTreeGraph graph, float x, float y) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, y, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        [Test]
        public void TransitionPlaceholdersDoNotBreakTheDump()
        {
            // Every transition created in the editor owns an invisible PlaceHolderNode so the transition
            // line can be selected. It has no ports and is never defined, and reading them used to throw
            // and abort the whole asset — so any hand-edited tree dumped nothing at all.
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, sequence);

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
            var cooldown = AddNode<Cooldown>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, cooldown);

            StringAssert.Contains("\"guid\": \"" + cooldown.guid + "\"", BehaviorTreeDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void GuardsReportTheirGuid()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, sequence);

            var guard = AddNode<BooleanConditionalExecution>(graph, -150.0f, 100.0f);
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
            var cooldown = AddNode<Cooldown>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, cooldown);

            Assert.IsTrue(cooldown.isDefined, "Adding a node to a graph must define its ports, as AfterAdd does in the editor.");
            Assert.IsNotNull(cooldown.Duration, "A defined Cooldown exposes its Duration port.");

            StringAssert.Contains("Cooldown", BehaviorTreeDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void ChildrenAreReportedInCanvasOrderNotInsertionOrder()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, selector);

            // added right first, so insertion order and canvas order disagree
            var right = AddNode<Repeater>(graph, 200.0f, 200.0f);
            var left = AddNode<WaitTime>(graph, -200.0f, 200.0f);
            Connect(graph, selector, right);
            Connect(graph, selector, left);

            string json = BehaviorTreeDump.ToJson(graph, "Fixture");

            Assert.Less(json.IndexOf("\"Wait\""), json.IndexOf("\"Repeater\""),
                "The runtime sorts children by canvas X, so the dump has to agree or it misreports priority.");
        }

        [Test]
        public void InlinePortValuesAreShown()
        {
            var graph = new BehaviorTreeGraph();
            var cooldown = AddNode<Cooldown>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, cooldown);

            cooldown.Duration.SetDefaultValue(7.5f);

            StringAssert.Contains("\"Duration\": \"7.5\"", BehaviorTreeDump.ToJson(graph, "Fixture"));
        }

        [Test]
        public void WiredPortsShowWhereTheValueComesFrom()
        {
            var graph = new BehaviorTreeGraph();
            var cooldown = AddNode<Cooldown>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, cooldown);

            var literal = AddNode<FloatLiteral>(graph, -200.0f, 100.0f);
            literal.Value.ValidlyConnectTo(cooldown.Duration);

            StringAssert.Contains("\"Duration\": \"<- Float Literal.Value\"", BehaviorTreeDump.ToJson(graph, "Fixture"),
                "A connected port must name its source rather than report an inline value.");
        }

        [Test]
        public void NodesNothingReachesAreCalledOut()
        {
            var graph = new BehaviorTreeGraph();
            AddNode<Sequence>(graph, 300.0f, 300.0f);

            StringAssert.Contains("unreachable", BehaviorTreeDump.ToJson(graph, "Fixture"),
                "A node left unconnected is the most common authoring mistake, so it must be visible.");
        }
    }
}

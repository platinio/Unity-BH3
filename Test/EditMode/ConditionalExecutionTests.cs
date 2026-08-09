using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Guards are the mechanism the whole priority scheme rests on: a branch states its precondition, and
    /// the moment that precondition stops holding the branch dies mid-run. <see cref="LogicNodeTests"/>
    /// covers a guard's own evaluation; these cover the integration — that a guard authored in code is
    /// actually attached to its owner, ANDs with its siblings, and aborts work already in progress.
    /// </summary>
    [TestFixture]
    public class ConditionalExecutionTests
    {
        private static T AddNode<T>(BehaviorTreeGraph graph, float x = 0.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, 0);
            graph.Transitions.Add(transition);
        }

        /// <summary>
        /// Builds Entry -> Sequence -> a child that never finishes, and returns the guarded Sequence.
        /// Guards only reach their owner through <see cref="BehaviorTreeGraph.OnAwake"/>, so callers must
        /// run it after wiring — that ordering is itself part of what these tests pin.
        /// </summary>
        private static Sequence GuardedSequence(BehaviorTreeGraph graph, out ScriptedNode child)
        {
            var sequence = AddNode<Sequence>(graph);

            child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            return sequence;
        }

        [Test]
        public void AGuardIsOnlyAttachedToItsOwnerByGraphOnAwake()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out _);

            var guard = AddNode<BooleanConditionalExecution>(graph);
            guard.UpdateOwner(sequence);

            Assert.IsEmpty(sequence.ConditionalExecutions,
                "UpdateOwner alone does not arm a guard; a generated tree that never runs OnAwake has none.");

            graph.OnAwake();

            Assert.AreEqual(1, sequence.ConditionalExecutions.Count);
        }

        [Test]
        public void AGuardTurningFalseAbortsABranchAlreadyRunning()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out var child);

            var guard = AddNode<BooleanConditionalExecution>(graph);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(true);

            graph.OnAwake();
            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "While the guard holds, the branch runs normally.");

            guard.Value.SetDefaultValue(false);

            Assert.AreEqual(ExecutionStatus.Failure, sequence.OnUpdateInternal(),
                "A guard going false must kill the branch on the same tick, not at the next loop.");
            Assert.AreEqual(1, child.UpdateCalls,
                "The child must not tick again once its branch has been invalidated.");
        }

        [Test]
        public void GuardsOnTheSameOwnerCombineAsAnAnd()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out _);

            var first = AddNode<BooleanConditionalExecution>(graph, -200.0f);
            var second = AddNode<BooleanConditionalExecution>(graph, 200.0f);
            first.UpdateOwner(sequence);
            second.UpdateOwner(sequence);
            first.Value.SetDefaultValue(true);
            second.Value.SetDefaultValue(true);

            graph.OnAwake();
            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "Both guards hold, so the branch runs.");

            second.Value.SetDefaultValue(false);

            Assert.AreEqual(ExecutionStatus.Failure, sequence.OnUpdateInternal(),
                "Any single guard failing must invalidate the branch. This is what lets a branch spell out "
                + "its full precondition instead of relying on sibling order.");
        }

        [Test]
        public void AFalseGuardStopsTheBranchBeingEnteredAtAll()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out var child);

            var guard = AddNode<BooleanConditionalExecution>(graph);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(false);

            graph.OnAwake();
            sequence.OnNodeEnter();

            Assert.AreEqual(0, child.EnterCalls,
                "A branch whose precondition already fails must never start.");
        }

        /// <summary>
        /// The priority scheme only works if declining a branch is free. A guarded-off branch reports Failure
        /// to its selector, and the selector has to treat that like any other failure — try the next one now,
        /// not next frame. With three gated branches above the one that runs, the old behaviour delayed the
        /// agent's actual decision by three frames.
        /// </summary>
        [Test]
        public void ASelectorSkipsGuardedOffBranchesWithoutSpendingAFrame()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var gatedOff = new ScriptedNode(ExecutionStatus.Success) { Position = new Rect(-200.0f, 300.0f, 150.0f, 100.0f) };
            var fallback = new ScriptedNode(ExecutionStatus.Success) { Position = new Rect(200.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(gatedOff);
            graph.Nodes.Add(fallback);

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, gatedOff);
            Connect(graph, selector, fallback);

            var guard = AddNode<BooleanConditionalExecution>(graph);
            guard.UpdateOwner(gatedOff);
            guard.Value.SetDefaultValue(false);

            graph.OnAwake();
            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal(),
                "The selector must fall past the gated branch and resolve the next one in the same tick.");
            Assert.AreEqual(0, gatedOff.UpdateCalls, "A branch whose precondition fails must never run.");
            Assert.AreEqual(0, gatedOff.EnterCalls, "Nor be entered.");
            Assert.AreEqual(1, fallback.UpdateCalls, "The branch that should run did, on the first tick.");
        }

        [Test]
        public void GuardsAreReportedUnderTheOwnerTheyProtect()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out _);

            var guard = AddNode<BooleanConditionalExecution>(graph);
            guard.UpdateOwner(sequence);

            string json = BehaviorTreeDump.ToJson(graph, "Fixture");

            StringAssert.Contains("\"guards\"", json);

            // scoped to the guarded node: the document's first "children" belongs to Entry, further up
            int sequenceStart = json.IndexOf("\"type\": \"Sequence\"");
            Assert.AreNotEqual(-1, sequenceStart, "The guarded branch should be in the dump.");

            Assert.Less(json.IndexOf("\"guards\"", sequenceStart), json.IndexOf("\"children\"", sequenceStart),
                "A guard reads as a precondition, so it belongs above the branch it gates.");
        }

        [Test]
        public void NothingStopsCodeFromOverParentingADecorator()
        {
            // The editor caps this in EndTransition via ReduceNodeTransitions. The authoring API does not,
            // and Decorator.OnEnter only ever enters child 0, so extra children are silently dead weight.
            // Documented as a hazard rather than a feature: generated trees have to respect the limit
            // themselves.
            var graph = new BehaviorTreeGraph();
            var repeater = AddNode<Repeater>(graph);

            var first = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(-100.0f, 300.0f, 150.0f, 100.0f) };
            var second = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(100.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(first);
            graph.Nodes.Add(second);

            Connect(graph, repeater, first);
            Connect(graph, repeater, second);
            graph.OnAwake();

            Assert.AreEqual(1, repeater.MaxChildrenLimit, "A decorator declares room for exactly one child.");
            Assert.AreEqual(2, repeater.GetChildren().Count,
                "Yet the graph accepted two. Authoring code must enforce MaxChildrenLimit itself.");
        }
    }
}

using ArcaneOnyx.BehaviorTree.Authoring;
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
    ///
    /// <para>
    /// There are two guard kinds and the difference is the point. A <see cref="ConditionalExecution"/> is a
    /// doorman: evaluated at entry and never again. A <see cref="ReactiveGuard"/> is a watchman: re-checked
    /// while its owner runs, and able to kill it. Every fixture below that flips a guard mid-run therefore
    /// uses the reactive kind — the same fixture written with a plain conditional would pass for the wrong
    /// reason, having never asked the guard a second time.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ConditionalExecutionTests
    {
        /// <summary>
        /// Builds Entry -> Sequence -> a child that never finishes, and returns the guarded Sequence.
        /// Guards only reach their owner through <see cref="BehaviorTreeGraph.OnAwake"/>, so callers must
        /// run it after wiring — that ordering is itself part of what these tests pin.
        /// </summary>
        private static Sequence GuardedSequence(BehaviorTreeGraph graph, out ScriptedNode child)
        {
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(graph, 0.0f, 100.0f);

            child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, sequence);
            BehaviorTreeAuthoring.Connect(graph, sequence, child);

            return sequence;
        }

        [Test]
        public void AGuardIsOnlyAttachedToItsOwnerByGraphOnAwake()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out _);

            var guard = BehaviorTreeAuthoring.AddNode<BooleanConditionalExecution>(graph, 0.0f, 100.0f);
            guard.UpdateOwner(sequence);

            Assert.IsEmpty(sequence.ConditionalExecutions,
                "UpdateOwner alone does not arm a guard; a generated tree that never runs OnAwake has none.");

            graph.OnAwake();

            Assert.AreEqual(1, sequence.ConditionalExecutions.Count);
        }

        /// <summary>
        /// The breaking change, stated as a test. A <see cref="ConditionalExecution"/> that admitted a
        /// branch no longer has any say over it, which is what makes the class usable for the gates it was
        /// always meant to express — a <c>RandomChance</c> that re-rolled every frame killed its own branch
        /// within one tick, and an expensive one-shot check could not be afforded per frame at all.
        /// </summary>
        [Test]
        public void AConditionalExecutionDoesNotAbortABranchItAlreadyAdmitted()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out var child);

            var guard = BehaviorTreeAuthoring.AddNode<BooleanConditionalExecution>(graph, 0.0f, 100.0f);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(true);

            graph.OnAwake();
            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal());

            guard.Value.SetDefaultValue(false);

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "the doorman checked once and stopped caring; interrupting is a ReactiveGuard's job");
            Assert.AreEqual(2, child.UpdateCalls, "and the branch keeps working");
        }

        /// <summary>
        /// A reactive guard with <c>StopsItsOwnBranch</c> off still gates entry but never interrupts: the
        /// committed swing. Unreachable with a single flag, which is why capability is two virtuals rather
        /// than one enum.
        /// </summary>
        [Test]
        public void AReactiveGuardThatDoesNotAbortLetsItsBranchFinish()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out var child);

            var guard = BehaviorTreeAuthoring.AddNode<CountingReactiveGuard>(graph, 0.0f, 100.0f);
            guard.UpdateOwner(sequence);
            guard.SetCapabilities(abortsOwner: false, preempts: true);

            graph.OnAwake();
            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal());

            guard.Result = false;

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "the swing is committed: it bids for control but finishes what it started");
            Assert.AreEqual(1, guard.Evaluations,
                "and it is not even asked while its owner runs — entry evaluated it once, the ticks skipped it");
        }

        [Test]
        public void AReactiveGuardTurningFalseAbortsABranchAlreadyRunning()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = GuardedSequence(graph, out var child);

            var guard = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(graph, 0.0f, 100.0f);
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

            var first = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(graph, -200.0f, 100.0f);
            var second = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(graph, 200.0f, 100.0f);
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

            var guard = BehaviorTreeAuthoring.AddNode<BooleanConditionalExecution>(graph, 0.0f, 100.0f);
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
            var selector = BehaviorTreeAuthoring.AddNode<Selector>(graph, 0.0f, 100.0f);

            var gatedOff = new ScriptedNode(ExecutionStatus.Success) { Position = new Rect(-200.0f, 300.0f, 150.0f, 100.0f) };
            var fallback = new ScriptedNode(ExecutionStatus.Success) { Position = new Rect(200.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(gatedOff);
            graph.Nodes.Add(fallback);

            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, selector);
            BehaviorTreeAuthoring.Connect(graph, selector, gatedOff);
            BehaviorTreeAuthoring.Connect(graph, selector, fallback);

            var guard = BehaviorTreeAuthoring.AddNode<BooleanConditionalExecution>(graph, 0.0f, 100.0f);
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

            var guard = BehaviorTreeAuthoring.AddNode<BooleanConditionalExecution>(graph, 0.0f, 100.0f);
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
            var repeater = BehaviorTreeAuthoring.AddNode<Repeater>(graph, 0.0f, 100.0f);

            var first = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(-100.0f, 300.0f, 150.0f, 100.0f) };
            var second = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(100.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(first);
            graph.Nodes.Add(second);

            BehaviorTreeAuthoring.Connect(graph, repeater, first);
            BehaviorTreeAuthoring.Connect(graph, repeater, second);
            graph.OnAwake();

            Assert.AreEqual(1, repeater.MaxChildrenLimit, "A decorator declares room for exactly one child.");
            Assert.AreEqual(2, repeater.GetChildren().Count,
                "Yet the graph accepted two. Authoring code must enforce MaxChildrenLimit itself.");
        }

        /// <summary>
        /// A guard that never got an owner can still be deleted.
        ///
        /// <para>
        /// Removal used to walk the graph and clear the index cache on <c>owner</c> once for every guard
        /// sharing it -- and a guard matches itself in that walk, so a null owner was dereferenced every
        /// time. Deleting an unarmed guard threw <see cref="System.NullReferenceException"/> instead of
        /// deleting it. Having an owner and being armed are separate steps, as
        /// <see cref="AGuardIsOnlyAttachedToItsOwnerByGraphOnAwake"/> pins, and nothing obliges the first
        /// to have happened before someone deletes the guard.
        /// </para>
        /// </summary>
        [Test]
        public void AGuardThatNeverGotAnOwnerCanStillBeDeleted()
        {
            var graph = new BehaviorTreeGraph();
            var guard = BehaviorTreeAuthoring.AddNode<CountingGuard>(graph, 0.0f, 100.0f);

            Assert.IsNull(guard.Owner, "the whole case: nothing ever gave it one");

            Assert.DoesNotThrow(() => graph.Nodes.Remove(guard),
                "deleting a guard cannot depend on whether it was ever attached to anything");

            CollectionAssert.DoesNotContain(graph.Nodes, guard);
        }

        /// <summary>
        /// Removing a guard renumbers the ones after it.
        ///
        /// <para>
        /// An index is a position among the owner's guards, so deleting one shifts every later guard down.
        /// The cache is keyed by guid and would otherwise keep answering with the position a guard held
        /// before. Dropping it is the whole job of the removal hook -- which used to do it by calling the
        /// same method on the same owner once per sibling.
        /// </para>
        /// </summary>
        [Test]
        public void RemovingAGuardRenumbersTheOnesAfterIt()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(graph, 0.0f, 100.0f);

            var first = BehaviorTreeAuthoring.AddNode<CountingGuard>(graph, 0.0f, 100.0f);
            var second = BehaviorTreeAuthoring.AddNode<CountingGuard>(graph, 0.0f, 100.0f);
            first.UpdateOwner(sequence);
            second.UpdateOwner(sequence);

            Assert.AreEqual(1, sequence.GetConditionalIndex(second),
                "second in graph order -- and asking is what fills the cache");

            graph.Nodes.Remove(first);

            Assert.AreEqual(0, sequence.GetConditionalIndex(second),
                "it is the only guard left, so it is first now; a cache surviving the removal would still "
                + "answer 1");
        }
    }
}

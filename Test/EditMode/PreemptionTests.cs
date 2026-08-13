using System.Linq;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A higher-priority branch taking control away from a lower-priority one that is already running.
    ///
    /// <para>
    /// This is what collapses the coupling guards used to force. <see cref="Selector"/> resumes at
    /// <c>currentExecutingChildIndex</c> and never re-checks the siblings above it, so the only interrupt
    /// available was the running branch's own guard — which meant every branch had to carry the negated
    /// preconditions of everything above it. The shipped Zombie demonstrates the failure directly: Chase is
    /// guarded by <c>hasTarget</c>, so when <c>targetInRange</c> flips true the zombie keeps chasing and
    /// Attack, though eligible, never gets a turn.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PreemptionTests
    {
        private static T AddNode<T>(BehaviorTreeGraph graph, float x = 0.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child, int index)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, index);
            graph.Transitions.Add(transition);
        }

        /// <summary>
        /// Entry -> Selector -> [high, low], both long-running, with a reactive guard on <c>high</c> that
        /// starts false. So the selector falls past <c>high</c> and settles on <c>low</c>, which is the state
        /// every test here starts from.
        /// </summary>
        private static Selector TwoBranches(
            out ScriptedNode high, out ScriptedNode low, out CountingReactiveGuard bid, out BehaviorTreeGraph graph)
        {
            graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            high = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(-200.0f, 300.0f, 150.0f, 100.0f) };
            low = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(200.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(high);
            graph.Nodes.Add(low);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, high, 0);
            Connect(graph, selector, low, 1);

            bid = AddNode<CountingReactiveGuard>(graph, -400.0f);
            bid.UpdateOwner(high);
            bid.Result = false;

            graph.OnAwake();
            selector.OnNodeEnter();

            return selector;
        }

        [Test]
        public void ALowerPriorityBranchRunsWhileTheBidIsFalse()
        {
            var selector = TwoBranches(out var high, out var low, out _, out _);

            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal());
            Assert.AreEqual(0, high.EnterCalls, "the guarded branch was refused");
            Assert.AreEqual(1, low.EnterCalls, "so the fallback took the slot");
        }

        /// <summary>Spec test 1: same-frame takeover, and the victim gets a real exit.</summary>
        [Test]
        public void AGuardTurningTrueTakesOverFromALowerPriorityBranch()
        {
            var selector = TwoBranches(out var high, out var low, out var bid, out _);

            selector.OnUpdateInternal();

            bid.Result = true;

            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal());

            Assert.AreEqual(1, high.EnterCalls, "the preemptor is entered on the same frame the bid turned true");
            Assert.AreEqual(1, high.UpdateCalls, "and ticked on it too, rather than starting a frame late");
            Assert.AreEqual(1, low.ExitCalls, "the victim must be exited, not merely abandoned mid-run");
            Assert.IsFalse(low.IsRunning);
        }

        /// <summary>
        /// Spec test 2, the eligibility trap. A child carrying a mix of guard kinds is only taken if
        /// <em>every</em> guard passes, not just the one that can bid. Polling the reactive guard alone would
        /// abort the victim, then fail the preemptor's real entry check, fall through, and restart the victim
        /// from scratch — killing it for nothing, potentially every tick.
        /// </summary>
        [Test]
        public void AVetoOnThePreemptorLeavesTheVictimUntouched()
        {
            var selector = TwoBranches(out var high, out var low, out var bid, out var graph);

            var veto = AddNode<BooleanConditionalExecution>(graph, -600.0f);
            veto.UpdateOwner(high);
            veto.Value.SetDefaultValue(false);
            graph.OnAwake();
            selector.OnNodeEnter();

            selector.OnUpdateInternal();
            bid.Result = true;

            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal());

            Assert.AreEqual(0, high.EnterCalls, "the veto is not a bid, but it does get a vote");
            Assert.AreEqual(0, low.ExitCalls, "and the victim must not be killed for a takeover that cannot happen");
            Assert.AreEqual(2, low.UpdateCalls, "it just keeps running");
        }

        /// <summary>
        /// Spec test 7: no scan on the frame the selector itself is entered — there is no victim yet.
        /// <para>
        /// The observable is not "which child ran": on its entry frame the selector picks its
        /// highest-priority eligible child anyway, which from outside looks identical to a takeover. What
        /// separates them is that a takeover has a victim — it exits a running branch and records a
        /// preemption. On the entry frame neither may happen.
        /// </para>
        /// </summary>
        [Test]
        public void NoPreemptionScanRunsOnTheSelectorsOwnEntryFrame()
        {
            var selector = TwoBranches(out var high, out var low, out var bid, out var graph);

            var recorder = new Debugging.BehaviorTreeFlightRecorder("Agent", "Tree", 64, 8);
            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }

            bid.Result = true;

            selector.OnUpdateInternal();

            Assert.AreEqual(1, high.EnterCalls, "the eligible branch runs, by ordinary selection");
            Assert.AreEqual(0, low.EnterCalls, "the lower-priority branch was never in the slot");
            Assert.AreEqual(0, low.ExitCalls, "so there was nothing to evict");
            CollectionAssert.IsEmpty(
                recorder.Events.Where(e => e.Kind == Debugging.BehaviorTreeEventKind.NodeTakenOver).ToList(),
                "and nothing may be recorded as a preemption when no branch lost a slot");
        }

        /// <summary>Spec test 8: two eligible preemptors, lower index wins.</summary>
        [Test]
        public void ThePreemptorWithTheHighestPriorityWins()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var first = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(-300.0f, 300.0f, 150.0f, 100.0f) };
            var second = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            var running = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(300.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(first);
            graph.Nodes.Add(second);
            graph.Nodes.Add(running);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, first, 0);
            Connect(graph, selector, second, 1);
            Connect(graph, selector, running, 2);

            var firstBid = AddNode<CountingReactiveGuard>(graph, -500.0f);
            firstBid.UpdateOwner(first);
            firstBid.Result = false;

            var secondBid = AddNode<CountingReactiveGuard>(graph, -450.0f);
            secondBid.UpdateOwner(second);
            secondBid.Result = false;

            graph.OnAwake();
            selector.OnNodeEnter();
            selector.OnUpdateInternal();

            firstBid.Result = true;
            secondBid.Result = true;

            selector.OnUpdateInternal();

            Assert.AreEqual(1, first.EnterCalls, "index order decides: the leftmost eligible child takes the slot");
            Assert.AreEqual(0, second.EnterCalls, "the scan stops at the first winner rather than comparing candidates");
        }

        /// <summary>
        /// A branch with no preempting guard is never polled, however many plain conditionals it carries.
        /// Opt-in is what keeps preemption free for trees that do not use it.
        /// </summary>
        [Test]
        public void ABranchWithNoPreemptingGuardIsNeverPolled()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);

            var high = new ScriptedNode(ExecutionStatus.Failure) { Position = new Rect(-200.0f, 300.0f, 150.0f, 100.0f) };
            var low = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(200.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(high);
            graph.Nodes.Add(low);

            Connect(graph, graph.EntryNode, selector, 0);
            Connect(graph, selector, high, 0);
            Connect(graph, selector, low, 1);

            var doorman = AddNode<CountingGuard>(graph, -400.0f);
            doorman.UpdateOwner(high);

            graph.OnAwake();
            selector.OnNodeEnter();
            selector.OnUpdateInternal();

            Assert.IsFalse(high.HasTakeOverGuard, "a plain conditional cannot bid");

            int afterFirstTick = doorman.Evaluations;
            selector.OnUpdateInternal();

            Assert.AreEqual(afterFirstTick, doorman.Evaluations,
                "so the scan never asks it, and a selector whose children never opted in pays only a bool check");
        }

        /// <summary>
        /// The recording has to name preemption as its own thing. "Your precondition stopped holding" and
        /// "something more important wanted the slot" are different answers to why a branch ended, and only
        /// the second one names a cause outside the branch.
        /// </summary>
        [Test]
        public void APreemptionIsRecordedAsItsOwnEventNamingBothSides()
        {
            var selector = TwoBranches(out var high, out var low, out var bid, out var graph);

            var recorder = new Debugging.BehaviorTreeFlightRecorder("Agent", "Tree", 64, 8);
            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }

            selector.OnUpdateInternal();
            bid.Result = true;
            selector.OnUpdateInternal();

            var preemption = recorder.Events
                .FirstOrDefault(e => e.Kind == Debugging.BehaviorTreeEventKind.NodeTakenOver);

            Assert.IsNotNull(preemption, "a takeover must be recorded as NodeTakenOver, not as a plain abort");
            Assert.AreEqual(low.guid, preemption.NodeGuid, "the event is about the branch that lost the slot");
            Assert.AreEqual(bid.guid, preemption.RelatedGuid, "and names the guard that made the bid");
            Assert.AreEqual(high.NodeName, preemption.Key, "and the preemptor, so an explanation can name it");
        }
    }
}

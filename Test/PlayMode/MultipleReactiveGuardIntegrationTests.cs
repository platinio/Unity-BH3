using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Two reactive guards on one node, ticking in a real player loop.
    ///
    /// <para>
    /// The edit-mode suite pins that guards on one owner are ANDed. What it cannot show is the reason two
    /// guards are worth having over one guard fed by an <c>And</c>: each carries its own trigger list, and so
    /// its own dirty flag. A distance polled on an interval and a fact woken by a key write cannot share a
    /// guard without one schedule being forced onto the other — and in edit mode neither schedule can fire,
    /// since key triggers have no writer to read and intervals have no clock.
    /// </para>
    ///
    /// <para>
    /// So this is one scenario, walked end to end: the guards recompute independently, the node still needs
    /// both to enter, either one alone can end the run, and the one that stays false holds the door shut no
    /// matter how often the other recomputes true.
    /// </para>
    /// </summary>
    public class MultipleReactiveGuardIntegrationTests : PlayModeAgentFixture
    {
        private const float FrameSeconds = 0.05f;

        /// <summary>Same reasoning as <c>GuardScheduleIntegrationTests</c>: off a frame boundary, four frames per fire.</summary>
        private const float Interval = 0.17f;
        private const int FramesPerFire = 4;

        private System.Guid guarded;
        private System.Guid fallback;
        private System.Guid keyGuard;
        private System.Guid intervalGuard;

        [TearDown]
        public void ReleaseTheClock()
        {
            Time.captureDeltaTime = 0.0f;
        }

        [UnityTest]
        public IEnumerator TwoReactiveGuardsOnOneNode_KeepTheirOwnSchedulesAndAreAnded()
        {
            var machine = SpawnDoublyGuarded(hasTarget: true, inRange: false);

            Time.captureDeltaTime = FrameSeconds;

            yield return Frames(3);

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, fallback), "One guard false is enough to keep the branch out,");
            Assert.IsFalse(Entered(recorder, guarded), "so the fallback holds the slot.");

            // Publish both facts once so the writer exists and each key trigger has seen its key. Without
            // this the key guard would stay quiet for the wrong reason -- no writer to read a version from --
            // and the count below would prove nothing about its schedule.
            PublishFact(machine, "hasTarget", true);
            PublishFact(machine, "inRange", false);

            yield return Frames(4);

            var byKey = RunningGuard(machine, keyGuard);
            var byInterval = RunningGuard(machine, intervalGuard);

            int keyStart = byKey.Evaluations;
            int intervalStart = byInterval.Evaluations;

            const int frames = 20;

            yield return Frames(frames);

            const int expected = frames / FramesPerFire;

            Assert.AreEqual(0, byKey.Evaluations - keyStart,
                "Nothing wrote the key it watches, so the key-triggered guard must not have recomputed. If it "
                + "did, its sibling's interval is leaking onto it and the two guards are not scheduled apart.");
            Assert.That(byInterval.Evaluations - intervalStart, Is.InRange(expected - 1, expected + 1),
                $"The interval guard must keep its own {Interval}s cadence beside a key guard that never wakes: "
                + $"about {expected} recomputes over {frames} frames. Zero means the sibling's clean flag is "
                + "silencing it; every frame means the interval is not gating anything.");

            int mark = Mark(recorder);
            PublishFact(machine, "inRange", true);

            yield return Frames(FramesPerFire * 2);

            Assert.IsTrue(EnteredSince(recorder, mark, guarded),
                "Once both guards hold the node takes the slot -- the interval guard recomputing true is what "
                + "completes the AND, and the key guard's cached true is what lets it through.");
            Assert.IsTrue(TakenOverSince(recorder, mark, fallback),
                "and it arrives as a takeover, since both guards default to Takes Over Lower Priority.");

            mark = Mark(recorder);
            PublishFact(machine, "hasTarget", false);

            yield return Frames(3);

            Assert.IsTrue(AbortedSince(recorder, mark, guarded),
                "Either guard turning false ends the run. The key guard woke on its own key here; the "
                + "interval guard was not consulted and did not need to be.");
            Assert.IsTrue(EnteredSince(recorder, mark, fallback), "and the slot falls through.");

            mark = Mark(recorder);

            yield return Frames(FramesPerFire * 3);

            Assert.IsFalse(EnteredSince(recorder, mark, guarded),
                "The interval guard keeps recomputing true on its cadence, and none of those wakes may reopen "
                + "the branch while the key guard still answers false. That is the AND holding under two "
                + "schedules, which is the whole reason two guards differ from one.");
        }

        /// <summary>
        /// A reactive guard beside a plain Conditional Execution on one node: the trigger and the veto.
        ///
        /// <para>
        /// The reactive guard is what makes the node pollable and what can end its run. The conditional gets
        /// a vote in the takeover poll but can neither cause a takeover nor abort — and the poll asking it at
        /// all is the whole correctness of preemption. Were only the reactive guard polled, the node would
        /// evict the running branch the moment its bid turned true, fail its own entry on the veto, fall
        /// through, and restart the victim from scratch, potentially every tick. Edit mode pins the veto
        /// half; this walks all four moments against a real machine, with the facts arriving as sensor writes.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AReactiveGuardBesideAConditional_TheConditionalVetoesButNeverFires()
        {
            var machine = SpawnTriggerAndVeto(targetInRange: false, hasToken: false);

            Time.captureDeltaTime = FrameSeconds;

            yield return Frames(3);

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, fallback), "With both guards false the fallback holds the slot.");
            Assert.IsFalse(Entered(recorder, guarded));

            int mark = Mark(recorder);
            PublishFact(machine, "targetInRange", true);

            yield return Frames(5);

            Assert.IsFalse(EnteredSince(recorder, mark, guarded),
                "The reactive guard's bid is true but the conditional says no, so the node must not enter,");
            Assert.IsFalse(TakenOverSince(recorder, mark, fallback),
                "and nothing may be recorded as losing a slot for a takeover that cannot happen.");
            Assert.AreEqual(1, EnterCount(recorder, fallback),
                "The victim must be left untouched -- not exited and restarted -- which is the failure a "
                + "poll that skipped the conditional would produce on every tick.");

            mark = Mark(recorder);
            PublishFact(machine, "hasToken", true);

            yield return Frames(3);

            Assert.IsTrue(EnteredSince(recorder, mark, guarded),
                "With the veto lifted the reactive guard's standing bid goes through. The conditional was "
                + "asked fresh in the poll -- a doorman has no cache -- so no trigger on it was needed.");
            Assert.IsTrue(TakenOverSince(recorder, mark, fallback),
                "and it arrives as a takeover, named by the reactive guard.");

            mark = Mark(recorder);
            PublishFact(machine, "hasToken", false);

            yield return Frames(5);

            Assert.IsFalse(AbortedSince(recorder, mark, guarded),
                "The conditional turning false mid-run must change nothing: it decided entry and stopped "
                + "caring, and the running-tick walk does not ask it.");
            Assert.IsFalse(EnteredSince(recorder, mark, fallback), "so the branch below stays out.");

            mark = Mark(recorder);
            PublishFact(machine, "targetInRange", false);

            yield return Frames(3);

            Assert.IsTrue(AbortedSince(recorder, mark, guarded),
                "The reactive guard is the one that can end the run, and it does when its own fact drops.");
            Assert.IsTrue(EnteredSince(recorder, mark, fallback), "and the slot falls through.");
        }

        #region Fixture

        /// <summary>
        /// The same two-branch selector, with the guarded branch carrying a reactive guard on
        /// <c>targetInRange</c> (key-triggered, default capabilities) beside a plain
        /// <see cref="BooleanConditionalExecution"/> on <c>hasToken</c> — the Attack shape from the docs,
        /// where the range is the trigger and the attack token is a veto.
        /// </summary>
        private BehaviorTreeMachine SpawnTriggerAndVeto(bool targetInRange, bool hasToken)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var guardedNode = Add<WaitTime>(graph, -400.0f, 500.0f);
            var fallbackNode = Add<WaitTime>(graph, 400.0f, 500.0f);

            FeedFloat(graph, guardedNode, guardedNode.Time, 999.0f);
            FeedFloat(graph, fallbackNode, fallbackNode.Time, 999.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, guardedNode);
            Connect(graph, selector, fallbackNode);

            var readRange = ReadAgentVariable(graph, "targetInRange", -900.0f, 300.0f);
            var trigger = Add<BooleanReactiveGuard>(graph, -600.0f, 300.0f);
            trigger.UpdateOwner(guardedNode);
            readRange.Value.ValidlyConnectTo(trigger.Value);
            trigger.AddTrigger(GuardTrigger.KeyChanged("targetInRange"));

            var readToken = ReadAgentVariable(graph, "hasToken", -900.0f, 400.0f);
            var veto = Add<BooleanConditionalExecution>(graph, -600.0f, 400.0f);
            veto.UpdateOwner(guardedNode);
            readToken.Value.ValidlyConnectTo(veto.Value);

            guarded = guardedNode.guid;
            fallback = fallbackNode.guid;

            return Spawn(tree, (_, variables) =>
            {
                variables.declarations.Set("targetInRange", targetInRange);
                variables.declarations.Set("hasToken", hasToken);
            });
        }

        /// <summary>
        /// Entry -&gt; Repeater -&gt; Selector -&gt; [ guarded, fallback ], both long-running. The guarded
        /// branch carries two reactive guards, both with default capabilities: one reads <c>hasTarget</c> and
        /// wakes when that key changes, the other reads <c>inRange</c> and wakes on an interval — the shape a
        /// melee attack has, with a fact from a sensor beside a distance nobody publishes a change event for.
        /// </summary>
        private BehaviorTreeMachine SpawnDoublyGuarded(bool hasTarget, bool inRange)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var guardedNode = Add<WaitTime>(graph, -400.0f, 500.0f);
            var fallbackNode = Add<WaitTime>(graph, 400.0f, 500.0f);

            FeedFloat(graph, guardedNode, guardedNode.Time, 999.0f);
            FeedFloat(graph, fallbackNode, fallbackNode.Time, 999.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, guardedNode);
            Connect(graph, selector, fallbackNode);

            var readTarget = ReadAgentVariable(graph, "hasTarget", -900.0f, 300.0f);
            var onKey = Add<BooleanReactiveGuard>(graph, -600.0f, 300.0f);
            onKey.UpdateOwner(guardedNode);
            readTarget.Value.ValidlyConnectTo(onKey.Value);
            onKey.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            var readRange = ReadAgentVariable(graph, "inRange", -900.0f, 400.0f);
            var onInterval = Add<BooleanReactiveGuard>(graph, -600.0f, 400.0f);
            onInterval.UpdateOwner(guardedNode);
            readRange.Value.ValidlyConnectTo(onInterval.Value);
            onInterval.AddTrigger(GuardTrigger.Interval(Interval));

            guarded = guardedNode.guid;
            fallback = fallbackNode.guid;
            keyGuard = onKey.guid;
            intervalGuard = onInterval.guid;

            return Spawn(tree, (_, variables) =>
            {
                variables.declarations.Set("hasTarget", hasTarget);
                variables.declarations.Set("inRange", inRange);
            });
        }

        /// <summary>
        /// The live clone of one specific guard. <see cref="PlayModeAgentFixture.RunningNode{T}"/> answers
        /// with the first of a type, which is ambiguous the moment a node carries two.
        /// </summary>
        private static ReactiveGuard RunningGuard(BehaviorTreeMachine machine, System.Guid guid)
        {
            Assert.IsNotNull(machine.RunningGraph, "The machine is not running a tree.");

            var guard = machine.RunningGraph.Nodes.OfType<ReactiveGuard>().FirstOrDefault(g => g.guid == guid);
            Assert.IsNotNull(guard, "The running graph must contain the guard the fixture authored.");

            return guard;
        }

        #endregion
    }
}

using System.Collections;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// The recorder as driven by a real machine — and, because the rest of this assembly asserts
    /// <em>through</em> the recording, the foundation the other fixtures stand on.
    ///
    /// <para>
    /// That coupling is deliberate and worth keeping: guids survive the machine's <c>Instantiate</c> of the
    /// macro, so a recording keyed by guid is the only observation surface that still refers to the nodes a
    /// designer authored. But it does mean a recorder fault would show up as a confusing failure — or a
    /// false pass — somewhere else entirely, so the properties those assertions lean on are pinned here
    /// rather than assumed.
    /// </para>
    ///
    /// <para>
    /// <b>Deliberately not a repeat of the edit-mode suite.</b> <c>BehaviorTreeFlightRecorderTests</c> already
    /// covers the ring's eviction and reporting, guard events, exit statuses, call-site identity and
    /// within-tick ordering — all by driving <c>BeginTick</c> and the nodes by hand. What it cannot cover is
    /// the half the <em>machine</em> supplies: that a tick is opened once per <c>Update</c>, and that a tree
    /// left running across real frames keeps producing a balanced, complete recording.
    /// </para>
    /// </summary>
    public class FlightRecorderIntegrationTests : PlayModeAgentFixture
    {
        /// <summary>
        /// One tick per <c>Update</c>, no more and no fewer.
        ///
        /// <para>
        /// <c>Machine.Update</c> calls <c>BeginTick</c> before running the tree, so everything the tree does
        /// in a frame is stamped with one number and ordered within it. That is what makes a recording
        /// readable as a timeline at all — and what every "this happened after that" assertion in the other
        /// fixtures is resting on. A second <c>BeginTick</c> per frame, or one skipped, would leave those
        /// comparisons quietly meaningless rather than failing.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheMachineOpensExactlyOneTickPerFrame()
        {
            var machine = SpawnTickingAgent(out _);

            yield return null;

            var recorder = machine.FlightRecorder;
            int before = recorder.Tick;

            const int frames = 8;

            for (int frame = 0; frame < frames; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(frames, recorder.Tick - before,
                "The tick counter has to track frames one for one. Drifting either way means events from "
                + "different frames share a number, or one frame's events are split across two.");

            Assert.IsFalse(recorder.Events.Any(e => e.Tick > recorder.Tick),
                "and nothing may be stamped with a tick that has not been opened yet.");
        }

        /// <summary>
        /// Enters and exits stay paired across a real player loop.
        ///
        /// <para>
        /// This is the property <c>EnterCount</c> is built on, and it is the one most easily broken from a
        /// distance: any node that reaches a child through <c>OnUpdateInternal</c> without entering it first
        /// produces an update with no enter, and any node entered twice without an exit between produces the
        /// opposite. The Cooldown defect found by the timing fixture was exactly the first shape, and it
        /// showed up as an entry count that would not grow.
        /// </para>
        ///
        /// <para>
        /// The counts used to differ by one, because the Repeater restarted its child at the <em>end</em> of
        /// each completing tick and so left it entered across the gap between two frames. That gap was the
        /// bug: a guard turning false inside it went unnoticed, and the next tick ran a child whose entry had
        /// been refused. Entry moved to <see cref="ContainerNode.TickChild"/>, so an entry now opens and
        /// closes inside one tick and the pairing is exact at any frame boundary — a stricter invariant than
        /// the one this replaced, and one that no longer depends on where in the tick the snapshot lands.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator EntersAndExitsStayPairedForANodeRestartedEveryFrame()
        {
            var machine = SpawnTickingAgent(out var child);

            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;

            int enters = EnterCount(recorder, child);
            int exits = ExitCount(recorder, child);

            Assert.Greater(enters, 2, "The Repeater has to have restarted the child several times.");
            Assert.AreEqual(enters, exits,
                $"A restarted node is entered and exited within one tick, so at a frame boundary no entry is "
                + $"left open. Saw {enters} enters against {exits} exits, which means an entry or an exit "
                + "went unrecorded -- and every count taken from this recording is then off by that much. "
                + "A surplus enter also means the child sat entered between two frames, which is the window "
                + "a guard change used to slip through.");
        }

        /// <summary>
        /// A run of the length these fixtures use stays well inside the ring.
        ///
        /// <para>
        /// A canary rather than a behaviour test. The ring keeps the newest events and only <c>Dropped</c>
        /// reports the loss, so a tree that starts emitting more per tick would not break anything visibly —
        /// it would quietly turn every count in this assembly into a lower bound, and the first symptom would
        /// be an unrelated fixture failing for a reason that makes no sense. This fails first, and says why.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AFixtureLengthRunStaysInsideTheRingWithMargin()
        {
            var machine = SpawnTickingAgent(out _);

            // Longer than any other fixture here runs.
            for (int frame = 0; frame < 25; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;

            Assert.AreEqual(0, recorder.Dropped,
                "Nothing may be evicted over a fixture-length run, or the counts the other tests take from "
                + "their recordings are lower bounds rather than answers.");

            Assert.Greater(recorder.EventCount, 0, "and it did record something, rather than passing by silence.");

            Assert.Less(recorder.EventCount, BehaviorTreeFlightRecorder.DefaultCapacity / 2,
                $"Margin has to stay comfortable: {recorder.EventCount} of "
                + $"{BehaviorTreeFlightRecorder.DefaultCapacity} after 25 frames. Approaching the capacity "
                + "means the suite is one longer test away from silently mis-counting.");
        }

        /// <summary>
        /// Entry -> Repeater -> Wait(0): a node that completes on the tick it runs, so the Repeater exits and
        /// re-enters it every frame. The simplest tree that produces a steady stream of paired events.
        /// </summary>
        private BehaviorTreeMachine SpawnTickingAgent(out System.Guid child)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var wait = Add<WaitTime>(graph, 0.0f, 250.0f);
            FeedFloat(graph, wait, wait.Time, 0.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, wait);

            child = wait.guid;

            return Spawn(tree);
        }
    }
}

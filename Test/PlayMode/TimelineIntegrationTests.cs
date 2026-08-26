using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// The timeline built from a recording a real machine produced, rather than from one assembled by hand.
    ///
    /// <para>
    /// The edit-mode suite covers the builder's rules thoroughly — lanes, nesting, aborts, sub-tree markers,
    /// clipping, and the agreement between <see cref="BehaviorTreeTimeline"/> and
    /// <see cref="BehaviorTreeTreeState"/> — but every recording it reads is one a <c>RecordingBuilder</c>
    /// wrote to order. That makes the whole component a pure function tested against its own idea of what a
    /// recording looks like, and a real recording is exactly what is best placed to violate that idea: call
    /// sites arrive from the machine instantiating a sub-tree asset rather than from
    /// <c>RegisterCallSite</c>, aborts arrive from a reactive guard waking on a version bump under real
    /// scheduling, and ticks arrive one per frame from the player loop.
    /// </para>
    ///
    /// <para>
    /// Nothing here is a new rule about timelines. Every rule is already covered. What was not covered is that
    /// the recorder and the timeline agree about the recordings the recorder actually writes.
    /// </para>
    /// </summary>
    public class TimelineIntegrationTests : PlayModeAgentFixture
    {
        private int capacityBeforeTest;

        [SetUp]
        public void RememberCapacity()
        {
            capacityBeforeTest = BehaviorTreeFlightRecorders.DefaultCapacity;
        }

        [TearDown]
        public void RestoreCapacity()
        {
            BehaviorTreeFlightRecorders.DefaultCapacity = capacityBeforeTest;
        }

        [UnityTest]
        public IEnumerator TwoRealCallSitesOfOneBranchStayTwoSegments()
        {
            // The edit-mode version of this pins the rule against call site ids handed to RegisterCallSite by
            // the test itself. Here the ids come from the machine instantiating the branch asset once per
            // call site — the mechanism the rule exists for. A guid-keyed timeline would show one branch in
            // two states at once, and both segments carry the same guid because a clone keeps it.
            var branch = BuildHoldingBranch(out var hold);

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var parallel = Add<ParallelSequence>(graph, 0.0f, 250.0f);
            var first = CallBranch(graph, branch, -400.0f, 400.0f);
            var second = CallBranch(graph, branch, 400.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, parallel);
            Connect(graph, parallel, first);
            Connect(graph, parallel, second);

            var machine = Spawn(tree);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var timeline = BehaviorTreeTimeline.Build(machine.FlightRecorder);
            var segments = SegmentsFor(timeline, hold);

            Assert.AreEqual(2, segments.Count,
                "One node guid, two running copies of the branch — so two bars, not one shared between them.");
            Assert.AreNotEqual(segments[0].CallSiteId, segments[1].CallSiteId,
                "and they are told apart by call site, which is the only thing that differs between them.");

            Assert.That(segments.Select(s => s.CallSiteId), Has.None.EqualTo(BehaviorTreeCallSite.RootId),
                "Both are inside a sub-tree, so neither belongs to the root call site.");
        }

        [UnityTest]
        public IEnumerator ARealGuardAbortProducesAnAbortedSegmentNamingTheGuard()
        {
            // An abort assembled by hand is one event; a real one is a reactive guard waking on a version
            // bump, answering false, killing a running branch, and the recorder emitting the abort followed
            // by that node's own exit in the same tick. The timeline has to fold those into one aborted
            // segment that still blames the guard — pinned in edit mode against a hand-written pair, and here
            // against the pair the machine really produces.
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);
            var attack = Add<WaitTime>(graph, -400.0f, 400.0f);
            var idle = Add<WaitTime>(graph, 400.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attack);
            Connect(graph, selector, idle);

            FeedFloat(graph, attack, attack.Time, 999.0f);
            FeedFloat(graph, idle, idle.Time, 999.0f);

            var read = ReadAgentVariable(graph, "hasTarget", -600.0f, 0.0f);
            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 250.0f);

            guard.UpdateOwner(attack);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            // Starts true, so the guarded branch is the one that runs and there is something to abort.
            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", true));

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(Entered(machine.FlightRecorder, attack.guid),
                "The guarded branch has to have started, or there is no abort to record.");

            PublishFact(machine, "hasTarget", false);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var timeline = BehaviorTreeTimeline.Build(machine.FlightRecorder);
            var aborted = SegmentsFor(timeline, attack.guid)
                .Where(segment => segment.Outcome == BehaviorTreeOutcome.Aborted)
                .ToList();

            Assert.AreEqual(1, aborted.Count, "The guard fell once, so the branch was killed once.");
            Assert.AreEqual(guard.guid, aborted[0].GuardGuid,
                "and the bar still names the guard that did it — the question a designer opens the scrubber to ask.");
        }

        [UnityTest]
        public IEnumerator ARealRunThatOverflowsTheRingProducesAnHonestTimeline()
        {
            // The play-mode suite otherwise checks that a fixture-length run stays *inside* the ring. This is
            // the other side: a run long enough to lose its own beginning. "The branch started here" and "the
            // buffer starts here" look identical on a timeline, so the count has to survive to the timeline,
            // and nothing may be drawn earlier than the oldest tick still held.
            BehaviorTreeFlightRecorders.DefaultCapacity = 32;

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var write = Add<SetVariable>(graph, 0.0f, 250.0f);

            SetPrivateField(write, "VariableKind", VariableKind.Object);
            FeedString(graph, write, write.Key, "spin");
            FeedBool(graph, write, write.Value, true);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, write);

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("spin", false));

            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;

            Assert.Greater(recorder.Dropped, 0,
                "The run has to have outgrown its ring for this to prove anything.");

            var timeline = BehaviorTreeTimeline.Build(recorder);

            Assert.AreEqual(recorder.Dropped, timeline.Dropped,
                "A clipped recording has to say so on the timeline, or the left edge reads as a beginning.");

            var segments = AllSegments(timeline);

            Assert.IsNotEmpty(segments,
                "Losing the oldest events must still leave a timeline of the ones it kept, or the checks below "
                + "pass by having nothing to check.");

            foreach (var segment in segments)
            {
                Assert.GreaterOrEqual(segment.EnterTick, timeline.FirstTick,
                    $"'{segment.Name}' starts before the oldest tick still in the buffer, which is history the "
                    + "recording no longer holds.");
            }

            var changeTicks = timeline.ChangeTicks;

            for (int i = 1; i < changeTicks.Count; i++)
            {
                Assert.Less(changeTicks[i - 1], changeTicks[i], "Change ticks are ascending and deduplicated.");
            }

            if (changeTicks.Count > 0)
            {
                Assert.GreaterOrEqual(changeTicks[0], timeline.FirstTick);
                Assert.LessOrEqual(changeTicks[changeTicks.Count - 1], timeline.LastTick);
            }
        }

        [UnityTest]
        public IEnumerator TreeStateAgreesWithTheTimelineOnARealRecording()
        {
            // The strongest claim the pair makes — a bar covering a tick and a node the canvas draws as idle
            // cannot both be right — checked against a recording nothing wrote to order. The two read the
            // same segments by design; this is what proves the design survives contact with a real run,
            // including the sub-tree boundaries and the abort below.
            var branch = BuildHoldingBranch(out _);

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);
            var guarded = CallBranch(graph, branch, -400.0f, 400.0f);
            var fallback = Add<WaitTime>(graph, 400.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, guarded);
            Connect(graph, selector, fallback);

            FeedFloat(graph, fallback, fallback.Time, 999.0f);

            var read = ReadAgentVariable(graph, "hasTarget", -600.0f, 0.0f);
            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 250.0f);

            guard.UpdateOwner(guarded);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("hasTarget", true));

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            // Aborting the call site abandons the branch inside it, which is the case that used to leave
            // nodes lit up forever when the two read the event stream separately.
            PublishFact(machine, "hasTarget", false);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;
            var timeline = BehaviorTreeTimeline.Build(recorder);

            Assert.IsNotEmpty(timeline.ChangeTicks, "There has to be something to scrub through.");

            int checkedBars = 0;

            foreach (var tick in timeline.ChangeTicks)
            {
                var state = BehaviorTreeTreeState.At(recorder, timeline, tick);

                foreach (var segment in AllSegments(timeline))
                {
                    var covered = segment.EnterTick <= tick && (segment.IsOpen || segment.ExitTick > tick);
                    if (!covered) continue;

                    checkedBars++;

                    Assert.IsTrue(state.IsRunning(segment.CallSiteId, segment.NodeGuid),
                        $"At tick {tick} the bar for '{segment.Name}' covers the playhead while the canvas "
                        + "would draw it idle. They are one source of truth or the scrubber contradicts itself.");
                }
            }

            Assert.Greater(checkedBars, 0,
                "No bar covered any change tick, so the agreement above was never actually tested.");
        }

        #region Building

        /// <summary>
        /// A branch that starts something and never finishes it, so it is still live when the test looks. No
        /// declarations, since what is under test is the call site rather than the parameter contract.
        /// </summary>
        private static BehaviorTreeGraphAsset BuildHoldingBranch(out Guid hold)
        {
            var asset = NewTree();
            var graph = asset.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 100.0f);

            FeedFloat(graph, wait, wait.Time, 999.0f);
            Connect(graph, graph.EntryNode, wait);

            hold = wait.guid;

            return asset;
        }

        /// <summary>
        /// A call site pointed at the branch. <c>RefreshParameters</c> is what the editor does on assignment;
        /// the ports come from a copy of the contract held on the calling node, so they exist only after it.
        /// </summary>
        private static RunBehaviorTreeGraphNode CallBranch(
            BehaviorTreeGraph graph, BehaviorTreeGraphAsset branch, float x, float y)
        {
            var call = Add<RunBehaviorTreeGraphNode>(graph, x, y);

            call.SetBehaviorTreeGraphAsset(branch);
            call.RefreshParameters();

            return call;
        }

        private static List<BehaviorTreeTimelineSegment> AllSegments(BehaviorTreeTimeline timeline) =>
            timeline.Lanes.SelectMany(lane => lane.Segments).ToList();

        private static List<BehaviorTreeTimelineSegment> SegmentsFor(BehaviorTreeTimeline timeline, Guid node) =>
            AllSegments(timeline).Where(segment => segment.NodeGuid == node).ToList();

        #endregion
    }
}

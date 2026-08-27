using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The scrubber's promise is that the picture at tick N is what was actually true at tick N, so these
    /// tests are mostly about time: that a segment ends where the recording says it ended, that scrubbing to a
    /// tick cannot see the future, and that the one ordering trap in the buffer — an abort and its own exit
    /// arriving on the same tick — produces one segment blaming the guard rather than two blaming nothing.
    ///
    /// <para>
    /// Fixtures are hand-built buffers for the same reason the why-inspector's are: the awkward cases (a
    /// clipped recording, two branches running at once, a re-enter whose exit was dropped) are a line of setup
    /// here and a whole scene in play mode. The cases that are really about the recorder's own output drive
    /// real nodes through the real recorder instead, so the engine and Component 1 cannot drift apart.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeTimelineTests
    {
        private static readonly Guid Root = new("aaaaaaaa-0000-0000-0000-000000000000");
        private static readonly Guid Branch = new("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Guard = new("22222222-2222-2222-2222-222222222222");
        private static readonly Guid Sibling = new("33333333-3333-3333-3333-333333333333");
        private static readonly Guid Child = new("55555555-5555-5555-5555-555555555555");
        private static readonly Guid RunNode = new("77777777-7777-7777-7777-777777777777");

        #region Fixtures

        private sealed class RecordingBuilder
        {
            private readonly List<BehaviorTreeEvent> events = new();
            private readonly List<BehaviorTreeCallSite> callSites = new()
            {
                new BehaviorTreeCallSite(BehaviorTreeCallSite.RootId, BehaviorTreeCallSite.RootId, Guid.Empty, "Zombie"),
            };

            private int tick;
            private int sequence;

            public RecordingBuilder At(int value)
            {
                tick = value;
                sequence = 0;
                return this;
            }

            public RecordingBuilder CallSite(int id, int parent, string asset)
            {
                callSites.Add(new BehaviorTreeCallSite(id, parent, RunNode, asset));
                return this;
            }

            public RecordingBuilder Enter(Guid node, int scope = 0) => Add(BehaviorTreeEventKind.NodeEnter, scope, node);

            public RecordingBuilder Exit(Guid node, ExecutionStatus status, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeExit, scope, node, status: status);

            public RecordingBuilder Aborted(Guid node, Guid guard, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeAborted, scope, node, guard);

            public RecordingBuilder TakenOver(Guid victim, Guid guard, string preemptorName, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeTakenOver, scope, victim, guard, key: preemptorName);

            public RecordingBuilder Skipped(Guid node, Guid guard, int scope = 0) =>
                Add(BehaviorTreeEventKind.NodeSkipped, scope, node, guard);

            public RecordingBuilder GuardEval(Guid guard, Guid owner, bool result, int scope = 0) =>
                Add(BehaviorTreeEventKind.GuardEval, scope, guard, owner, flag: result);

            public RecordingBuilder Pushed(Guid runNode, string asset, int scope = 0) =>
                Add(BehaviorTreeEventKind.TreePushed, scope, runNode, key: asset);

            public RecordingBuilder Popped(Guid runNode, string asset, int scope = 0) =>
                Add(BehaviorTreeEventKind.TreePopped, scope, runNode, key: asset);

            private RecordingBuilder Add(
                BehaviorTreeEventKind kind, int scope, Guid node, Guid related = default,
                ExecutionStatus status = ExecutionStatus.None, bool flag = false, string key = null)
            {
                events.Add(BehaviorTreeEvent.Create(
                    kind, tick, sequence++, tick, tick * 0.02f, scope, node, related, status, flag, key));

                return this;
            }

            public BehaviorTreeRecordingSnapshot Build(int dropped = 0) =>
                new("Zombie", "ZombieTree", tick, events, callSites, dropped, new List<GuardTrace>());
        }

        private sealed class StubTopology : IBehaviorTreeTopology
        {
            private readonly Dictionary<Guid, BehaviorTreeNodeInfo> nodes = new();

            public StubTopology Node(Guid guid, string name)
            {
                nodes[guid] = new BehaviorTreeNodeInfo(guid, name, "ScriptedNode", Guid.Empty, Array.Empty<Guid>());
                return this;
            }

            public bool TryGetNode(Guid guid, out BehaviorTreeNodeInfo node) => nodes.TryGetValue(guid, out node);

            public bool TryGetGuardReads(Guid guard, out IReadOnlyList<string> keys)
            {
                keys = Array.Empty<string>();
                return false;
            }
        }

        private static IEnumerable<BehaviorTreeTimelineSegment> AllSegments(BehaviorTreeTimeline timeline)
        {
            return timeline.Lanes.SelectMany(lane => lane.Segments);
        }

        private static BehaviorTreeTimelineSegment SegmentFor(BehaviorTreeTimeline timeline, Guid node)
        {
            return AllSegments(timeline).Single(segment => segment.NodeGuid == node);
        }

        #endregion

        #region Segments

        [Test]
        public void ASegmentSpansTheTicksItsNodeWasActive()
        {
            var recording = new RecordingBuilder()
                .At(100).Enter(Branch)
                .At(140).Exit(Branch, ExecutionStatus.Success)
                .Build();

            var segment = SegmentFor(BehaviorTreeTimeline.Build(recording), Branch);

            Assert.AreEqual(100, segment.EnterTick);
            Assert.AreEqual(140, segment.ExitTick);
            Assert.AreEqual(BehaviorTreeOutcome.Succeeded, segment.Outcome);
            Assert.IsFalse(segment.IsOpen);
        }

        [Test]
        public void ANodeStillRunningAtTheEndStaysOpenRatherThanBeingGivenAnInventedEnd()
        {
            var recording = new RecordingBuilder()
                .At(100).Enter(Branch)
                .At(180)
                .Build();

            var segment = SegmentFor(BehaviorTreeTimeline.Build(recording), Branch);

            Assert.IsTrue(segment.IsOpen,
                "'Still going' and 'stopped at the last tick' are different answers and the timeline must not "
                + "conflate them — one is a branch working, the other is a branch that died silently.");
            Assert.AreEqual(BehaviorTreeOutcome.Running, segment.Outcome);
            Assert.AreEqual(180, segment.EndTickOr(180));
        }

        [Test]
        public void NestedNodesLandOnDeeperLanes()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Root).Enter(Branch).Enter(Child)
                .At(20).Exit(Child, ExecutionStatus.Success).Exit(Branch, ExecutionStatus.Success)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);

            Assert.AreEqual(0, SegmentFor(timeline, Root).Depth);
            Assert.AreEqual(1, SegmentFor(timeline, Branch).Depth);
            Assert.AreEqual(2, SegmentFor(timeline, Child).Depth);
        }

        [Test]
        public void SiblingsTakingTurnsShareOneLane()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Root).Enter(Branch)
                .At(20).Exit(Branch, ExecutionStatus.Failure)
                .At(21).Enter(Sibling)
                .At(30).Exit(Sibling, ExecutionStatus.Success)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);
            var lane = timeline.Lanes.Single(candidate => candidate.Depth == 1);

            Assert.AreEqual(2, lane.Segments.Count,
                "Branches that never overlap are the ordinary case and belong on one row — a lane per branch "
                + "would make a normal selector look like a parallel one.");
        }

        [Test]
        public void ConcurrentNodesGetTheirOwnLaneRatherThanOverlapping()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Root).Enter(Branch).Enter(Sibling)
                .At(40).Exit(Branch, ExecutionStatus.Success).Exit(Sibling, ExecutionStatus.Success)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);

            foreach (var lane in timeline.Lanes)
            {
                for (int i = 1; i < lane.Segments.Count; i++)
                {
                    Assert.GreaterOrEqual(lane.Segments[i].EnterTick, lane.Segments[i - 1].EndTickOr(timeline.LastTick),
                        "Two bars drawn on top of each other hide one of them at exactly the zoom someone is "
                        + "using to find it. Starting exactly where the previous one ended is a handoff, not "
                        + "an overlap, and sharing a lane is what keeps the timeline readable.");
                }
            }

            Assert.AreEqual(2, AllSegments(timeline).Count(segment => segment.EnterTick == 10 && segment.NodeGuid != Root),
                "Both concurrent nodes must still be present — spilling to another lane is a layout decision, "
                + "not a licence to drop one.");
        }

        #endregion

        #region Aborts

        [Test]
        public void AnAbortEndsTheSegmentAndNamesTheGuard()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412).GuardEval(Guard, Branch, false).Aborted(Branch, Guard)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);
            var segment = SegmentFor(timeline, Branch);

            Assert.AreEqual(BehaviorTreeOutcome.Aborted, segment.Outcome);
            Assert.AreEqual(Guard, segment.GuardGuid,
                "The guard is the answer the scrubber exists to surface; losing it leaves 'it stopped'.");

            var marker = timeline.Markers.Single(candidate => candidate.Kind == BehaviorTreeTimelineMarkerKind.Abort);

            Assert.AreEqual(412, marker.Tick);
            Assert.AreEqual(Guard, marker.RelatedGuid);
        }

        [Test]
        public void AnAbortAndItsOwnExitProduceOneSegmentThatStillBlamesTheGuard()
        {
            // Finding 8: the runtime exits an aborted node on the same tick, and the exit is recorded second.
            // Taken at face value the last event is a plain NodeExit Failure — true, and useless.
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412).Aborted(Branch, Guard).Exit(Branch, ExecutionStatus.Failure)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);
            var segments = AllSegments(timeline).Where(segment => segment.NodeGuid == Branch).ToArray();

            Assert.AreEqual(1, segments.Length,
                "The trailing exit belongs to the abort. Treating it as a separate episode invents a "
                + "zero-length segment and reports a dropped enter that never happened.");
            Assert.AreEqual(BehaviorTreeOutcome.Aborted, segments[0].Outcome);
            Assert.AreEqual(Guard, segments[0].GuardGuid);
        }

        [Test]
        public void ATakenOverSegmentIsNotColouredAsAnAbort()
        {
            // Same shape as an abort — the bar ends early and the node's own exit follows on the tick — but a
            // different claim: nothing under this branch turned false, a sibling outbid it. A bar that reads
            // as an abort sends whoever is scrubbing to look inside a branch where there is nothing to find.
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(450).TakenOver(Branch, Guard, "Attack").Exit(Branch, ExecutionStatus.Failure)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);
            var segments = AllSegments(timeline).Where(segment => segment.NodeGuid == Branch).ToArray();

            Assert.AreEqual(1, segments.Length, "The trailing exit belongs to the takeover, as it does to an abort.");
            Assert.AreEqual(BehaviorTreeOutcome.TakenOver, segments[0].Outcome);
            Assert.AreEqual(400, segments[0].EnterTick);
            Assert.AreEqual(450, segments[0].ExitTick);
            Assert.AreEqual(Guard, segments[0].GuardGuid, "The guard that won the slot is still worth carrying.");

            Assert.IsEmpty(timeline.Markers.Where(m => m.Kind == BehaviorTreeTimelineMarkerKind.Abort).ToArray(),
                "The abort pin means 'a guard killed this', which is the opposite of what happened.");
        }

        [Test]
        public void ATakeoverEndsEverythingInsideTheBranchItStopped()
        {
            // The abandoned-sub-tree rule, applied to the other way a running branch can be stopped. Nodes
            // inside a sub-tree record no exit of their own when their caller loses the slot, so without this
            // they stay open and the ghosted canvas lights them up for the rest of the recording.
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .At(10).Enter(Root).Enter(RunNode).Pushed(RunNode, "Attack")
                .At(12).Enter(Child, scope: 1)
                .At(50).TakenOver(RunNode, Guard, "Flee").Exit(RunNode, ExecutionStatus.Failure)
                .At(80).Enter(Sibling)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);
            var inside = SegmentFor(timeline, Child);

            Assert.IsFalse(inside.IsOpen,
                "The branch inside stopped when its caller lost the slot, whatever it did or did not record.");
            Assert.AreEqual(50, inside.ExitTick);

            var state = BehaviorTreeTreeState.At(recording, timeline, 80);

            Assert.IsFalse(state.IsRunning(1, Child),
                "and the canvas agrees — a node still lit thirty ticks after its tree was abandoned is the "
                + "failure one source of truth exists to prevent.");
        }

        [Test]
        public void ASkippedNodeGetsNoSegment()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Root)
                .At(12).Skipped(Branch, Guard)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);

            Assert.IsFalse(AllSegments(timeline).Any(segment => segment.NodeGuid == Branch),
                "A branch that never entered has no duration. Drawing it would claim the tree ran something "
                + "it explicitly declined to run.");
        }

        #endregion

        #region Sub-trees

        [Test]
        public void SubTreeBoundariesBecomeMarkers()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .At(50).Enter(Root).Pushed(RunNode, "Attack")
                .At(52).Enter(Child, scope: 1)
                .At(90).Exit(Child, ExecutionStatus.Success, scope: 1).Popped(RunNode, "Attack")
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);

            var pushed = timeline.Markers.Single(m => m.Kind == BehaviorTreeTimelineMarkerKind.TreePushed);
            var popped = timeline.Markers.Single(m => m.Kind == BehaviorTreeTimelineMarkerKind.TreePopped);

            Assert.AreEqual(50, pushed.Tick);
            Assert.AreEqual("Attack", pushed.Label);
            Assert.AreEqual(90, popped.Tick);
        }

        [Test]
        public void TheSameBranchAtTwoCallSitesStaysTwoSegments()
        {
            // The case the whole (CallSiteId, NodeGuid) addressing exists for: a sub-tree asset is instantiated
            // per call site and the clone keeps the original guids, so guid alone shows one node twice.
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack").CallSite(2, 0, "Attack")
                .At(10).Enter(Child, scope: 1)
                .At(20).Exit(Child, ExecutionStatus.Success, scope: 1)
                .At(30).Enter(Child, scope: 2)
                .At(40).Exit(Child, ExecutionStatus.Failure, scope: 2)
                .Build();

            var segments = AllSegments(BehaviorTreeTimeline.Build(recording))
                .Where(segment => segment.NodeGuid == Child)
                .OrderBy(segment => segment.EnterTick)
                .ToArray();

            Assert.AreEqual(2, segments.Length);
            Assert.AreEqual(1, segments[0].CallSiteId);
            Assert.AreEqual(2, segments[1].CallSiteId);
            Assert.AreEqual(BehaviorTreeOutcome.Succeeded, segments[0].Outcome);
            Assert.AreEqual(BehaviorTreeOutcome.Failed, segments[1].Outcome,
                "Two call sites of one branch can end differently, and collapsing them hides exactly that.");
        }

        [Test]
        public void AbortingTheRunNodeEndsEverythingInsideTheSubTree()
        {
            // The recorder blames the node in the *caller's* graph and abandons the instance's own nodes with
            // no exit each. Nothing else closes them, so without a teardown they stay open to the end of the
            // recording — found by running the demo, where a 1.5s Wait drew as a bar thousands of ticks long.
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .At(10).Enter(Root).Enter(RunNode).Pushed(RunNode, "Attack")
                .At(12).Enter(Child, scope: 1)
                .At(50).Aborted(RunNode, Guard)
                .At(200)
                .Build();

            var inside = AllSegments(BehaviorTreeTimeline.Build(recording))
                .Single(segment => segment.NodeGuid == Child);

            Assert.IsFalse(inside.IsOpen,
                "A sub-tree cannot outlive the node running it, so its nodes cannot still be running at the "
                + "end of the recording when that node died at tick 50.");
            Assert.AreEqual(50, inside.ExitTick);
            Assert.AreEqual(BehaviorTreeOutcome.Aborted, inside.Outcome);
        }

        [Test]
        public void DepthDoesNotDriftAsTheRecordingGrows()
        {
            // Depth used to be a count of everything currently open, which climbs forever in a tree whose root
            // nodes stay entered — so the same node drew one lane lower every time its branch restarted.
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .At(10).Enter(Root).Enter(RunNode)
                .At(11).Enter(Child, scope: 1)
                .At(50).Aborted(RunNode, Guard)
                .At(60).Enter(RunNode)
                .At(61).Enter(Child, scope: 1)
                .At(90).Exit(Child, ExecutionStatus.Success, scope: 1)
                .Build();

            var visits = AllSegments(BehaviorTreeTimeline.Build(recording))
                .Where(segment => segment.NodeGuid == Child)
                .OrderBy(segment => segment.EnterTick)
                .ToArray();

            Assert.AreEqual(2, visits.Length);
            Assert.AreEqual(visits[0].Depth, visits[1].Depth,
                "The same node in the same structural position must land on the same lane every time it runs, "
                + "or the timeline grows a new lane per branch entry and stops being readable.");
        }

        [Test]
        public void ASubTreesNodesSitBelowTheNodeThatRunsIt()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .At(10).Enter(Root).Enter(RunNode)
                .At(11).Enter(Child, scope: 1)
                .At(90).Exit(Child, ExecutionStatus.Success, scope: 1)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);

            Assert.Greater(SegmentFor(timeline, Child).Depth, SegmentFor(timeline, RunNode).Depth,
                "Nesting is the one thing a lane number has to convey.");
        }

        #endregion

        #region Navigation

        [Test]
        public void ChangeTicksHoldOnlyTheTicksWhereSomethingHappened()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Root)
                .At(50).Enter(Branch)
                .At(90).Exit(Branch, ExecutionStatus.Success)
                .At(200)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);

            CollectionAssert.AreEqual(new[] { 10, 50, 90 }, timeline.ChangeTicks.ToArray(),
                "Stepping one tick at a time mostly shows the same picture; jumping between the ticks where "
                + "the active set actually changed is what makes a long recording usable.");
        }

        [Test]
        public void AClippedRecordingSaysSo()
        {
            var recording = new RecordingBuilder()
                .At(100).Enter(Branch)
                .At(140).Exit(Branch, ExecutionStatus.Success)
                .Build(dropped: 37);

            Assert.AreEqual(37, BehaviorTreeTimeline.Build(recording).Dropped,
                "'The branch started here' and 'the buffer starts here' look identical on a timeline, and only "
                + "one of them is a fact.");
        }

        [Test]
        public void NamesComeFromTheTopologyWhenThereIsOneAndGuidsWhenThereIsNot()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Branch)
                .At(20).Exit(Branch, ExecutionStatus.Success)
                .Build();

            var named = SegmentFor(BehaviorTreeTimeline.Build(recording, new StubTopology().Node(Branch, "Idle")), Branch);
            var bare = SegmentFor(BehaviorTreeTimeline.Build(recording), Branch);

            Assert.AreEqual("Idle", named.Name);
            Assert.IsNotEmpty(bare.Name,
                "An imported recording with no tree to hand is terser, not broken — it still has to draw.");
        }

        #endregion

        #region Tree state at a tick

        [Test]
        public void StateAtATickReportsWhatWasRunningThen()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Root).Enter(Branch)
                .At(50).Exit(Branch, ExecutionStatus.Success).Enter(Sibling)
                .At(90).Exit(Sibling, ExecutionStatus.Failure)
                .Build();

            var atThirty = BehaviorTreeTreeState.At(recording, 30);

            Assert.AreEqual(ExecutionStatus.Running, atThirty.StatusOf(0, Branch));
            Assert.AreEqual(ExecutionStatus.None, atThirty.StatusOf(0, Sibling),
                "Sibling had not entered yet at tick 30, and a status it acquired later must not leak backwards.");

            var atSixty = BehaviorTreeTreeState.At(recording, 60);

            Assert.AreEqual(ExecutionStatus.Success, atSixty.StatusOf(0, Branch));
            Assert.AreEqual(ExecutionStatus.Running, atSixty.StatusOf(0, Sibling));
        }

        [Test]
        public void StateAtATickCannotSeeTheFuture()
        {
            var recording = new RecordingBuilder()
                .At(10).Enter(Branch)
                .At(90).Exit(Branch, ExecutionStatus.Failure)
                .Build();

            Assert.AreEqual(ExecutionStatus.Running, BehaviorTreeTreeState.At(recording, 50).StatusOf(0, Branch),
                "The whole point of scrubbing is to see the moment as it was. Leaking the outcome backwards "
                + "would answer a question the designer has not asked yet.");
        }

        [Test]
        public void StateIncludesEverythingRecordedOnTheTickScrubbedTo()
        {
            var recording = new RecordingBuilder()
                .At(400).Enter(Branch)
                .At(412).GuardEval(Guard, Branch, false).Aborted(Branch, Guard)
                .Build();

            var state = BehaviorTreeTreeState.At(recording, 412);

            Assert.AreEqual(ExecutionStatus.Failure, state.StatusOf(0, Branch),
                "A tick is the unit in which the tree makes a decision — showing the instant before it would "
                + "answer a question nobody asked.");
            Assert.IsTrue(state.WasAborted(0, Branch));
            Assert.AreEqual(false, state.GuardResult(0, Guard));
        }

        [Test]
        public void StateTellsCallSitesApartButStillAnswersWithoutOne()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack").CallSite(2, 0, "Attack")
                .At(10).Enter(Child, scope: 1).Enter(Child, scope: 2)
                .At(20).Exit(Child, ExecutionStatus.Success, scope: 1)
                .Build();

            var state = BehaviorTreeTreeState.At(recording, 20);

            Assert.AreEqual(ExecutionStatus.Success, state.StatusOf(1, Child));
            Assert.AreEqual(ExecutionStatus.Running, state.StatusOf(2, Child));

            Assert.AreEqual(ExecutionStatus.Running, state.StatusOf(Child),
                "The canvas knows which node was clicked but not which copy of a shared branch, so the "
                + "guid-only answer prefers the one still running.");
        }

        [Test]
        public void AnAbandonedSubTreeNodeIsNotStillRunningLater()
        {
            // The canvas is ghosted from this, so a node left Running here lights up on screen thousands of
            // ticks after its branch died. State used to replay the events on its own and disagreed with the
            // timeline for exactly this case.
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .At(10).Enter(Root).Enter(RunNode).Pushed(RunNode, "Attack")
                .At(12).Enter(Child, scope: 1)
                .At(50).Aborted(RunNode, Guard)
                .At(400)
                .Build();

            var state = BehaviorTreeTreeState.At(recording, 300);

            Assert.IsFalse(state.IsRunning(1, Child),
                "Its branch was killed at tick 50. Showing it as running at tick 300 is the debugger telling "
                + "the designer something that never happened.");
            Assert.AreEqual(ExecutionStatus.Failure, state.StatusOf(1, Child));
        }

        [Test]
        public void StateAgreesWithTheTimelineAtEveryChangeTick()
        {
            var recording = new RecordingBuilder()
                .CallSite(1, 0, "Attack")
                .At(10).Enter(Root).Enter(RunNode).Pushed(RunNode, "Attack")
                .At(12).Enter(Child, scope: 1)
                .At(50).Aborted(RunNode, Guard)
                .At(60).Enter(RunNode).Enter(Child, scope: 1)
                .At(90).Exit(Child, ExecutionStatus.Success, scope: 1)
                .Build();

            var timeline = BehaviorTreeTimeline.Build(recording);

            foreach (var tick in timeline.ChangeTicks)
            {
                var state = BehaviorTreeTreeState.At(recording, timeline, tick);

                foreach (var lane in timeline.Lanes)
                {
                    foreach (var segment in lane.Segments)
                    {
                        var covered = segment.EnterTick <= tick && (segment.IsOpen || segment.ExitTick > tick);
                        if (!covered) continue;

                        Assert.IsTrue(state.IsRunning(segment.CallSiteId, segment.NodeGuid),
                            $"A bar covering tick {tick} and a node the canvas draws as idle cannot both be "
                            + "right. They are one source of truth or the scrubber contradicts itself.");
                    }
                }
            }
        }

        #endregion

        #region Against the real recorder

        /// <summary>
        /// A real guarded Sequence, entered and then killed by its guard, recorded by the real recorder.
        /// Shared by the tests below so the round trip is exercised against genuine event shapes rather than
        /// against a buffer this file wrote itself.
        /// </summary>
        private static BehaviorTreeFlightRecorder RecordARealAbort(out Guid ownerGuid, out Guid guardGuid)
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;

            var recorder = new BehaviorTreeFlightRecorder("TestAgent", "TestTree");
            var graph = new BehaviorTreeGraph();

            var sequence = new Sequence { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(sequence);

            var child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            var toSequence = new BehaviorTreeTransition();
            toSequence.SetupTransition(graph.EntryNode, sequence, 0);
            graph.Transitions.Add(toSequence);

            var toChild = new BehaviorTreeTransition();
            toChild.SetupTransition(sequence, child, 0);
            graph.Transitions.Add(toChild);

            var guard = new BooleanReactiveGuard { Position = new Rect(0.0f, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(guard);
            guard.UpdateOwner(sequence);
            guard.Value.SetDefaultValue(true);

            graph.OnAwake();

            foreach (var node in graph.Nodes) node.SetFlightRecorder(recorder);

            recorder.BeginTick();
            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();

            recorder.BeginTick();
            guard.Value.SetDefaultValue(false);
            sequence.OnUpdateInternal();

            ownerGuid = sequence.guid;
            guardGuid = guard.guid;

            return recorder;
        }

        [Test]
        public void ARealAbortProducesARealAbortedSegment()
        {
            // Driving actual nodes through the actual recorder, so a change to either side of the hooks shows
            // up here rather than silently producing a timeline that disagrees with the tree.
            var recorder = RecordARealAbort(out var ownerGuid, out var guardGuid);

            var timeline = BehaviorTreeTimeline.Build(recorder);
            var segment = AllSegments(timeline).Single(candidate => candidate.NodeGuid == ownerGuid);

            Assert.AreEqual(BehaviorTreeOutcome.Aborted, segment.Outcome,
                "This is the shape the demo reproduces; if the recorder's event order changes, the timeline "
                + "must fail here rather than quietly drawing a Failure.");
            Assert.AreEqual(guardGuid, segment.GuardGuid);

            BehaviorTreeFlightRecorders.Reset();
        }

        [Test]
        public void ARoundTrippedRecordingScrubsIdentically()
        {
            var original = RecordARealAbort(out _, out _);

            var reloaded = BehaviorTreeRecordingImport.FromJson(BehaviorTreeRecordingDump.ToJson(original));

            var before = BehaviorTreeTimeline.Build(original);
            var after = BehaviorTreeTimeline.Build(reloaded);

            var beforeSegments = AllSegments(before).OrderBy(s => s.EnterTick).ThenBy(s => s.Depth).ToArray();
            var afterSegments = AllSegments(after).OrderBy(s => s.EnterTick).ThenBy(s => s.Depth).ToArray();

            Assert.AreEqual(beforeSegments.Length, afterSegments.Length);

            for (int i = 0; i < beforeSegments.Length; i++)
            {
                Assert.AreEqual(beforeSegments[i].EnterTick, afterSegments[i].EnterTick);
                Assert.AreEqual(beforeSegments[i].ExitTick, afterSegments[i].ExitTick);
                Assert.AreEqual(beforeSegments[i].Outcome, afterSegments[i].Outcome);
                Assert.AreEqual(beforeSegments[i].GuardGuid, afterSegments[i].GuardGuid);
            }

            CollectionAssert.AreEqual(before.ChangeTicks.ToArray(), after.ChangeTicks.ToArray(),
                "Acceptance criterion 3: an exported recording has to reproduce the same scrubber view, or "
                + "'a recording plus a tree dump is a complete bug report' is not true.");
        }

        #endregion
    }
}

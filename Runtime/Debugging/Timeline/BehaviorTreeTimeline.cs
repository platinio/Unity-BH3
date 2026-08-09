using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A recording turned into something drawable: lanes of segments, pins for the things that happen at an
    /// instant, and the ticks worth jumping between.
    ///
    /// <para>
    /// Pure C# and built from an <see cref="IBehaviorTreeRecording"/>, exactly like
    /// <see cref="BehaviorTreeExplainer"/> and for the same reason — a recording exported from someone else's
    /// playtest scrubs as well as the one running in front of you, and a test can assert on the model without
    /// standing up an editor. The UI is a renderer of this and holds no opinion of its own about what happened.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeTimeline
    {
        private readonly List<BehaviorTreeTimelineLane> lanes = new();
        private readonly List<BehaviorTreeTimelineMarker> markers = new();
        private readonly List<int> changeTicks = new();

        private BehaviorTreeTimeline(string agentName, string treeName, int firstTick, int lastTick, int dropped)
        {
            AgentName = agentName;
            TreeName = treeName;
            FirstTick = firstTick;
            LastTick = lastTick;
            Dropped = dropped;
        }

        public string AgentName { get; }

        public string TreeName { get; }

        /// <summary>The oldest tick still in the buffer.</summary>
        public int FirstTick { get; }

        /// <summary>The newest tick recorded. The scrubber's right-hand edge.</summary>
        public int LastTick { get; }

        /// <summary>
        /// Events that fell off the back of the ring. Non-zero means the left edge is a cut, not a beginning —
        /// worth saying out loud, because "the branch started here" and "the buffer starts here" look identical
        /// on a timeline and only one of them is a fact.
        /// </summary>
        public int Dropped { get; }

        /// <summary>Shallowest first.</summary>
        public IReadOnlyList<BehaviorTreeTimelineLane> Lanes => lanes;

        public IReadOnlyList<BehaviorTreeTimelineMarker> Markers => markers;

        /// <summary>
        /// Ticks at which the set of active nodes changed — something entered, exited or was aborted.
        ///
        /// <para>
        /// This is what "jump to the next branch change" steps through, and it is the control that makes a
        /// long recording usable: in a typical tree the active set is unchanged for the overwhelming majority
        /// of ticks, so stepping one tick at a time mostly shows the same picture. Ascending, deduplicated.
        /// </para>
        /// </summary>
        public IReadOnlyList<int> ChangeTicks => changeTicks;

        /// <summary>Nothing was recorded, so there is nothing to scrub.</summary>
        public bool IsEmpty => lanes.Count == 0 && markers.Count == 0;

        /// <summary>
        /// Replays the recording into lanes and pins.
        ///
        /// <para>
        /// <paramref name="topology"/> is optional and supplies names only. Everything structural here comes
        /// from the event stream, so an imported recording with no tree to hand produces the same lanes with
        /// guids where the names would be — terser, not degraded.
        /// </para>
        /// </summary>
        public static BehaviorTreeTimeline Build(IBehaviorTreeRecording recording, IBehaviorTreeTopology topology = null)
        {
            if (recording == null) throw new ArgumentNullException(nameof(recording));

            var count = recording.EventCount;

            var firstTick = count > 0 ? recording.EventAt(0).Tick : 0;
            var timeline = new BehaviorTreeTimeline(
                recording.AgentName, recording.TreeName, firstTick, recording.Tick, recording.Dropped);

            timeline.Replay(recording, topology);

            return timeline;
        }

        private void Replay(IBehaviorTreeRecording recording, IBehaviorTreeTopology topology)
        {
            var replay = new Replayer(recording, topology, markers);

            replay.Run();

            foreach (var segment in replay.Segments)
            {
                LaneFor(segment).Add(segment);
            }

            lanes.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            changeTicks.AddRange(replay.ChangeTicks);
        }

        /// <summary>
        /// The lane a segment belongs in: its own depth when that lane is free at this tick, otherwise the next
        /// lane at the same depth. Segments arrive in enter order, so only the last one in a lane can conflict.
        /// </summary>
        private BehaviorTreeTimelineLane LaneFor(BehaviorTreeTimelineSegment segment)
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i].Depth != segment.Depth) continue;
                if (lanes[i].Accepts(segment.EnterTick, LastTick)) return lanes[i];
            }

            var lane = new BehaviorTreeTimelineLane(segment.Depth);
            lanes.Add(lane);

            return lane;
        }

        internal static int CompareByEnterThenDepth(BehaviorTreeTimelineSegment a, BehaviorTreeTimelineSegment b)
        {
            var byEnter = a.EnterTick.CompareTo(b.EnterTick);

            return byEnter != 0 ? byEnter : a.Depth.CompareTo(b.Depth);
        }

        internal static BehaviorTreeOutcome OutcomeOf(ExecutionStatus status)
        {
            switch (status)
            {
                case ExecutionStatus.Success: return BehaviorTreeOutcome.Succeeded;
                case ExecutionStatus.Failure: return BehaviorTreeOutcome.Failed;
                case ExecutionStatus.Running: return BehaviorTreeOutcome.Running;
                default: return BehaviorTreeOutcome.NoRecord;
            }
        }

        internal static string NameOf(IBehaviorTreeTopology topology, Guid guid)
        {
            if (guid == Guid.Empty) return null;

            if (topology != null && topology.TryGetNode(guid, out var info) && !string.IsNullOrEmpty(info.DisplayName))
            {
                return info.DisplayName;
            }

            return guid.ToString("N").Substring(0, 8);
        }

        /// <summary>
        /// Walks the event stream once and turns it into segments.
        ///
        /// <para>
        /// Two things here are not obvious and were both found by running a real agent rather than by reading
        /// the code. First, <b>depth is per scope, not global</b>: a count of everything currently open climbs
        /// forever in a tree that keeps its root nodes entered, so the timeline grew a new lane for every
        /// branch entry. Second, <b>a call site dies with the node that runs it</b> — see
        /// <see cref="CloseCallSitesUnder"/>.
        /// </para>
        /// </summary>
        private sealed class Replayer
        {
            private readonly IBehaviorTreeRecording recording;
            private readonly IBehaviorTreeTopology topology;
            private readonly List<BehaviorTreeTimelineMarker> markers;

            private readonly Dictionary<NodeKey, OpenSegment> open = new();
            private readonly Dictionary<int, HashSet<NodeKey>> openByCallSite = new();
            private readonly Dictionary<int, int> callSiteDepth = new();
            private readonly Dictionary<int, List<BehaviorTreeCallSite>> callSitesByParent = new();

            // An aborted node exits on the same tick and the exit is recorded second (Finding 8). The abort
            // already closed the segment, so that trailing exit is swallowed rather than treated as an orphan.
            private readonly Dictionary<NodeKey, int> abortedAt = new();

            private readonly SortedSet<int> changed = new();

            public Replayer(
                IBehaviorTreeRecording recording,
                IBehaviorTreeTopology topology,
                List<BehaviorTreeTimelineMarker> markers)
            {
                this.recording = recording;
                this.topology = topology;
                this.markers = markers;

                foreach (var callSite in recording.CallSites)
                {
                    if (callSite.IsRoot) continue;

                    if (!callSitesByParent.TryGetValue(callSite.ParentId, out var siblings))
                    {
                        siblings = new List<BehaviorTreeCallSite>();
                        callSitesByParent[callSite.ParentId] = siblings;
                    }

                    siblings.Add(callSite);
                }

                callSiteDepth[BehaviorTreeCallSite.RootId] = 0;
            }

            public List<BehaviorTreeTimelineSegment> Segments { get; } = new();

            public IEnumerable<int> ChangeTicks => changed;

            public void Run()
            {
                for (int i = 0; i < recording.EventCount; i++)
                {
                    var recorded = recording.EventAt(i);
                    var key = new NodeKey(recorded.CallSiteId, recorded.NodeGuid);

                    switch (recorded.Kind)
                    {
                        case BehaviorTreeEventKind.NodeEnter:
                            Enter(recorded, key, i);
                            break;

                        case BehaviorTreeEventKind.NodeAborted:
                            Abort(recorded, key, i);
                            break;

                        case BehaviorTreeEventKind.NodeExit:
                            Exit(recorded, key, i);
                            break;

                        case BehaviorTreeEventKind.TreePushed:
                        case BehaviorTreeEventKind.TreePopped:
                            Boundary(recorded, key, i);
                            break;
                    }
                }

                // Whatever is still running when the recording ends stays open rather than being given an
                // invented end: "still going" and "stopped at the last tick" are different answers.
                foreach (var pair in open)
                {
                    Segments.Add(pair.Value.Close(
                        BehaviorTreeTimelineSegment.StillOpen, BehaviorTreeOutcome.Running, Guid.Empty, -1));
                }

                Segments.Sort(CompareByEnterThenDepth);
            }

            private void Enter(BehaviorTreeEvent recorded, NodeKey key, int index)
            {
                abortedAt.Remove(key);

                // A re-enter without an exit means the exit was dropped. Close the stale one where the new one
                // starts rather than letting it swallow the gap.
                if (open.TryGetValue(key, out var stale))
                {
                    Segments.Add(stale.Close(recorded.Tick, BehaviorTreeOutcome.NoRecord, Guid.Empty, -1));
                    Forget(key);
                }

                var depth = DepthOf(recorded.CallSiteId);

                open[key] = new OpenSegment(key, NameOf(topology, recorded.NodeGuid), recorded.Tick, depth, index);
                Remember(key);

                // A node that runs a sub-tree sits one level above everything inside it, whatever the buffer
                // happens to have open elsewhere.
                if (callSitesByParent.TryGetValue(recorded.CallSiteId, out var children))
                {
                    foreach (var child in children)
                    {
                        if (child.RunNodeGuid == recorded.NodeGuid) callSiteDepth[child.Id] = depth + 1;
                    }
                }

                changed.Add(recorded.Tick);
            }

            private void Abort(BehaviorTreeEvent recorded, NodeKey key, int index)
            {
                abortedAt[key] = recorded.Tick;

                if (open.TryGetValue(key, out var aborting))
                {
                    Forget(key);

                    Segments.Add(aborting.Close(
                        recorded.Tick, BehaviorTreeOutcome.Aborted, recorded.RelatedGuid, index));

                    markers.Add(new BehaviorTreeTimelineMarker(
                        BehaviorTreeTimelineMarkerKind.Abort,
                        recorded.Tick,
                        recorded.CallSiteId,
                        recorded.NodeGuid,
                        recorded.RelatedGuid,
                        NameOf(topology, recorded.RelatedGuid),
                        aborting.Depth,
                        index));

                    CloseCallSitesUnder(recorded.CallSiteId, recorded.NodeGuid, recorded.Tick,
                        BehaviorTreeOutcome.Aborted, recorded.RelatedGuid);
                }

                changed.Add(recorded.Tick);
            }

            private void Exit(BehaviorTreeEvent recorded, NodeKey key, int index)
            {
                if (open.TryGetValue(key, out var exiting))
                {
                    Forget(key);
                    Segments.Add(exiting.Close(recorded.Tick, OutcomeOf(recorded.Status), Guid.Empty, index));

                    CloseCallSitesUnder(recorded.CallSiteId, recorded.NodeGuid, recorded.Tick,
                        BehaviorTreeOutcome.NoRecord, Guid.Empty);

                    changed.Add(recorded.Tick);
                    return;
                }

                if (abortedAt.TryGetValue(key, out var abortTick) && abortTick == recorded.Tick)
                {
                    abortedAt.Remove(key);
                }
            }

            private void Boundary(BehaviorTreeEvent recorded, NodeKey key, int index)
            {
                markers.Add(new BehaviorTreeTimelineMarker(
                    recorded.Kind == BehaviorTreeEventKind.TreePushed
                        ? BehaviorTreeTimelineMarkerKind.TreePushed
                        : BehaviorTreeTimelineMarkerKind.TreePopped,
                    recorded.Tick,
                    recorded.CallSiteId,
                    recorded.NodeGuid,
                    Guid.Empty,
                    recorded.Key,
                    open.TryGetValue(key, out var running) ? running.Depth : DepthOf(recorded.CallSiteId),
                    index));

                changed.Add(recorded.Tick);
            }

            /// <summary>
            /// Ends everything inside the call sites a node was running.
            ///
            /// <para>
            /// A sub-tree cannot outlive the <see cref="RunBehaviorTreeGraphNode"/> that runs it, but the
            /// recorder does not say so: when a guard aborts the run node, the abort is recorded against the
            /// node in the *caller's* graph and the instance's own nodes are simply abandoned, with no exit
            /// each. Without this they stay open for the rest of the recording, which drew a Wait as a bar
            /// thousands of ticks long and inflated the depth of everything entered afterwards.
            /// </para>
            /// </summary>
            private void CloseCallSitesUnder(int callSiteId, Guid runNodeGuid, int tick, BehaviorTreeOutcome outcome, Guid guard)
            {
                if (!callSitesByParent.TryGetValue(callSiteId, out var children)) return;

                foreach (var child in children)
                {
                    if (child.RunNodeGuid != runNodeGuid) continue;
                    if (!openByCallSite.TryGetValue(child.Id, out var inside) || inside.Count == 0) continue;

                    foreach (var key in new List<NodeKey>(inside))
                    {
                        if (!open.TryGetValue(key, out var abandoned)) continue;

                        Forget(key);
                        Segments.Add(abandoned.Close(tick, outcome, guard, -1));

                        // A branch can call a branch, so the teardown has to reach all the way down.
                        CloseCallSitesUnder(child.Id, key.NodeGuid, tick, outcome, guard);
                    }
                }
            }

            /// <summary>
            /// Where a node entering in this scope sits: the scope's own level, plus whatever is already open
            /// inside it. Per scope rather than global, so a root that stays entered forever does not push
            /// every later branch one lane further down.
            /// </summary>
            private int DepthOf(int callSiteId)
            {
                var baseDepth = callSiteDepth.TryGetValue(callSiteId, out var known) ? known : FallbackDepth(callSiteId);

                return baseDepth + (openByCallSite.TryGetValue(callSiteId, out var inside) ? inside.Count : 0);
            }

            /// <summary>
            /// How deep a call site sits when its run node's entry was never seen — a clipped buffer, or a
            /// recording that starts mid-branch. Counting the call-site chain is coarser than watching the
            /// node enter, but it keeps the lanes ordered instead of collapsing them all onto zero.
            /// </summary>
            private int FallbackDepth(int callSiteId)
            {
                var depth = 0;
                var current = callSiteId;

                for (int guard = 0; guard < recording.CallSites.Count; guard++)
                {
                    var found = false;

                    foreach (var callSite in recording.CallSites)
                    {
                        if (callSite.Id != current || callSite.IsRoot) continue;

                        current = callSite.ParentId;
                        depth += 2;
                        found = true;
                        break;
                    }

                    if (!found) break;
                }

                callSiteDepth[callSiteId] = depth;

                return depth;
            }

            private void Remember(NodeKey key)
            {
                if (!openByCallSite.TryGetValue(key.CallSiteId, out var inside))
                {
                    inside = new HashSet<NodeKey>();
                    openByCallSite[key.CallSiteId] = inside;
                }

                inside.Add(key);
            }

            private void Forget(NodeKey key)
            {
                open.Remove(key);

                if (openByCallSite.TryGetValue(key.CallSiteId, out var inside)) inside.Remove(key);
            }
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            public readonly int CallSiteId;
            public readonly Guid NodeGuid;

            public NodeKey(int callSiteId, Guid nodeGuid)
            {
                CallSiteId = callSiteId;
                NodeGuid = nodeGuid;
            }

            public bool Equals(NodeKey other) => CallSiteId == other.CallSiteId && NodeGuid.Equals(other.NodeGuid);

            public override bool Equals(object obj) => obj is NodeKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return (CallSiteId * 397) ^ NodeGuid.GetHashCode();
                }
            }
        }

        private readonly struct OpenSegment
        {
            private readonly NodeKey key;
            private readonly string name;
            private readonly int enterTick;
            private readonly int enterIndex;

            public readonly int Depth;

            public OpenSegment(NodeKey key, string name, int enterTick, int depth, int enterIndex)
            {
                this.key = key;
                this.name = name;
                this.enterTick = enterTick;
                this.enterIndex = enterIndex;
                Depth = depth;
            }

            public BehaviorTreeTimelineSegment Close(int exitTick, BehaviorTreeOutcome outcome, Guid guard, int exitIndex)
            {
                return new BehaviorTreeTimelineSegment(
                    key.CallSiteId, key.NodeGuid, name, enterTick, exitTick, outcome, guard, Depth, enterIndex, exitIndex);
            }
        }
    }
}

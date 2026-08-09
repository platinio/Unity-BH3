using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// What every node's status was at one tick — the thing the canvas paints when the scrubber is parked.
    ///
    /// <para>
    /// Reconstructed by replaying the event stream up to that tick rather than by snapshotting during
    /// recording. That is the whole reason the flight recorder stores events instead of snapshots: a snapshot
    /// per tick would cost the hot path a full tree walk to serve a question only a reader ever asks, while a
    /// replay costs nothing until someone scrubs.
    /// </para>
    ///
    /// <para>
    /// It is a value, not a view onto live nodes. Nothing here writes to a <see cref="BehaviorTreeNode"/>:
    /// the live tree keeps running while you study the past, and a debugger that edits the thing it is
    /// measuring is not one.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeTreeState
    {
        private readonly Dictionary<NodeKey, ExecutionStatus> statuses = new();
        private readonly Dictionary<Guid, ExecutionStatus> byGuid = new();
        private readonly HashSet<NodeKey> aborted = new();
        private readonly Dictionary<NodeKey, bool> guards = new();

        private BehaviorTreeTreeState(int tick)
        {
            Tick = tick;
        }

        /// <summary>The tick this state describes.</summary>
        public int Tick { get; }

        /// <summary>
        /// The state as of <paramref name="tick"/>, including everything recorded on that tick.
        ///
        /// <para>
        /// Inclusive because a tick is the unit in which the tree makes a decision: "the guard flipped and the
        /// branch aborted" both happen on the tick you scrubbed to, and showing the moment before them would
        /// answer a question nobody asked.
        /// </para>
        /// </summary>
        public static BehaviorTreeTreeState At(IBehaviorTreeRecording recording, int tick)
        {
            if (recording == null) throw new ArgumentNullException(nameof(recording));

            return At(recording, BehaviorTreeTimeline.Build(recording), tick);
        }

        /// <summary>
        /// The same, reusing a timeline that has already been built. The scrubber holds one and rebuilds it
        /// only when the recording moves, so scrubbing does not replay the ring twice per repaint.
        /// </summary>
        public static BehaviorTreeTreeState At(IBehaviorTreeRecording recording, BehaviorTreeTimeline timeline, int tick)
        {
            if (recording == null) throw new ArgumentNullException(nameof(recording));
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));

            var state = new BehaviorTreeTreeState(tick);

            state.ApplySegments(timeline);
            state.ApplyGuards(recording, tick);

            return state;
        }

        /// <summary>
        /// Node statuses come from the timeline's segments rather than from a second walk of the event stream.
        ///
        /// <para>
        /// They used to be replayed here independently, and the two disagreed: a sub-tree abandoned by an
        /// aborted branch records no exit for its own nodes, so a private replay left them Running for the
        /// rest of the recording and the ghosted canvas lit up nodes that had stopped thousands of ticks
        /// earlier. The timeline already resolves that, and one source of truth is the only way the bar a
        /// designer clicks and the node that lights up can be guaranteed to agree.
        /// </para>
        /// </summary>
        private void ApplySegments(BehaviorTreeTimeline timeline)
        {
            var ordered = new List<BehaviorTreeTimelineSegment>();

            foreach (var lane in timeline.Lanes)
            {
                foreach (var segment in lane.Segments)
                {
                    if (segment.EnterTick <= Tick) ordered.Add(segment);
                }
            }

            // Enter order, so a node that ran several times ends up holding its most recent status.
            ordered.Sort((a, b) => a.EnterTick.CompareTo(b.EnterTick));

            foreach (var segment in ordered)
            {
                var key = new NodeKey(segment.CallSiteId, segment.NodeGuid);
                var ended = !segment.IsOpen && segment.ExitTick <= Tick;

                if (!ended)
                {
                    aborted.Remove(key);
                    Set(key, ExecutionStatus.Running);
                    continue;
                }

                if (segment.Outcome == BehaviorTreeOutcome.Aborted) aborted.Add(key);
                else aborted.Remove(key);

                Set(key, StatusOf(segment.Outcome));
            }
        }

        private void ApplyGuards(IBehaviorTreeRecording recording, int tick)
        {
            for (int i = 0; i < recording.EventCount; i++)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Tick > tick) break;
                if (recorded.Kind != BehaviorTreeEventKind.GuardEval) continue;

                guards[new NodeKey(recorded.CallSiteId, recorded.NodeGuid)] = recorded.Flag;
            }
        }

        /// <summary>
        /// What the live canvas would have drawn. An aborted node reads Failure because that is what the
        /// runtime sets on it, not because the debugger decided so.
        /// </summary>
        private static ExecutionStatus StatusOf(BehaviorTreeOutcome outcome)
        {
            switch (outcome)
            {
                case BehaviorTreeOutcome.Succeeded: return ExecutionStatus.Success;
                case BehaviorTreeOutcome.Failed: return ExecutionStatus.Failure;
                case BehaviorTreeOutcome.Aborted: return ExecutionStatus.Failure;
                case BehaviorTreeOutcome.Running: return ExecutionStatus.Running;
                default: return ExecutionStatus.None;
            }
        }

        private void Set(NodeKey key, ExecutionStatus status)
        {
            statuses[key] = status;

            // A guid-only lookup exists because the canvas knows which node was clicked but not which copy of a
            // shared branch it belongs to. Running wins, so a node active in any call site reads as active.
            if (byGuid.TryGetValue(key.NodeGuid, out var existing) && existing == ExecutionStatus.Running)
            {
                if (status != ExecutionStatus.Running) return;
            }

            byGuid[key.NodeGuid] = status;
        }

        /// <summary>The status of one node in one call site.</summary>
        public ExecutionStatus StatusOf(int callSiteId, Guid nodeGuid)
        {
            return statuses.TryGetValue(new NodeKey(callSiteId, nodeGuid), out var status)
                ? status
                : ExecutionStatus.None;
        }

        /// <summary>
        /// The status of a node regardless of call site, preferring <see cref="ExecutionStatus.Running"/>.
        /// What the canvas uses when no particular call site has been picked.
        /// </summary>
        public ExecutionStatus StatusOf(Guid nodeGuid)
        {
            return byGuid.TryGetValue(nodeGuid, out var status) ? status : ExecutionStatus.None;
        }

        public bool IsRunning(int callSiteId, Guid nodeGuid) => StatusOf(callSiteId, nodeGuid) == ExecutionStatus.Running;

        public bool IsRunning(Guid nodeGuid) => StatusOf(nodeGuid) == ExecutionStatus.Running;

        /// <summary>Whether this node was killed by a guard at or before this tick, and has not re-entered.</summary>
        public bool WasAborted(int callSiteId, Guid nodeGuid) => aborted.Contains(new NodeKey(callSiteId, nodeGuid));

        /// <summary>A guard's last recorded result, or null when it never evaluated.</summary>
        public bool? GuardResult(int callSiteId, Guid guardGuid)
        {
            return guards.TryGetValue(new NodeKey(callSiteId, guardGuid), out var result) ? result : (bool?)null;
        }

        /// <summary>How many nodes were running. Zero usually means the tree had stopped ticking.</summary>
        public int RunningCount
        {
            get
            {
                var running = 0;

                foreach (var pair in statuses)
                {
                    if (pair.Value == ExecutionStatus.Running) running++;
                }

                return running;
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
    }
}

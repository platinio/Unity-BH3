using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One horizontal track of non-overlapping segments.
    ///
    /// <para>
    /// A lane is normally one nesting depth, which is what makes the timeline readable top-to-bottom: the root
    /// runs the whole time on lane 0, its branches take turns on lane 1, and their children on lane 2. Two
    /// lanes may share a <see cref="Depth"/> when a composite runs children concurrently — segments inside one
    /// lane never overlap, so a second lane is the only honest way to draw two nodes active at once. Drawing
    /// them on top of each other would hide one of them at exactly the zoom someone is using to find it.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeTimelineLane
    {
        private readonly List<BehaviorTreeTimelineSegment> segments = new();

        public BehaviorTreeTimelineLane(int depth)
        {
            Depth = depth;
        }

        /// <summary>Nesting level this lane represents. Not unique — see the type remarks.</summary>
        public int Depth { get; }

        /// <summary>Oldest first, never overlapping.</summary>
        public IReadOnlyList<BehaviorTreeTimelineSegment> Segments => segments;

        internal void Add(BehaviorTreeTimelineSegment segment) => segments.Add(segment);

        /// <summary>
        /// Whether a segment starting at <paramref name="enterTick"/> can be appended without overlapping.
        /// Only the last segment is checked because segments are added in enter order.
        ///
        /// <para>
        /// Starting on the tick the previous segment ended is allowed. That is a handoff, not an overlap — one
        /// branch failing and the next being tried on the same tick is the ordinary behaviour of a selector,
        /// and treating it as concurrency doubled the number of lanes on every real recording.
        /// </para>
        /// </summary>
        internal bool Accepts(int enterTick, int recordingTick)
        {
            if (segments.Count == 0) return true;

            var last = segments[segments.Count - 1];

            // An open segment runs to the end of the recording, so nothing can follow it in this lane.
            if (last.IsOpen) return false;

            return enterTick >= last.EndTickOr(recordingTick);
        }

        /// <summary>The segment covering <paramref name="tick"/>, if any.</summary>
        public bool TryGetAt(int tick, int recordingTick, out BehaviorTreeTimelineSegment segment)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (!segments[i].Covers(tick, recordingTick)) continue;

                segment = segments[i];
                return true;
            }

            segment = default;
            return false;
        }
    }
}

using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A moment worth a pin on the timeline rather than a bar: the things that happen *at* a tick instead of
    /// lasting for a stretch of them.
    /// </summary>
    public enum BehaviorTreeTimelineMarkerKind
    {
        /// <summary>A guard turned false and killed a running branch. Carries the guard in <c>RelatedGuid</c>.</summary>
        Abort,

        /// <summary>A <see cref="RunBehaviorTreeGraphNode"/> entered a sub-tree.</summary>
        TreePushed,

        /// <summary>A sub-tree finished and control came back to the caller.</summary>
        TreePopped,
    }

    /// <summary>
    /// One pin on the timeline. Deliberately not a segment: an abort has no duration, and drawing it as a
    /// zero-width bar makes it invisible at exactly the zoom level someone is using to find it.
    /// </summary>
    public readonly struct BehaviorTreeTimelineMarker
    {
        public readonly BehaviorTreeTimelineMarkerKind Kind;

        public readonly int Tick;

        public readonly int CallSiteId;

        /// <summary>The node this happened to.</summary>
        public readonly Guid NodeGuid;

        /// <summary>The guard for an <see cref="BehaviorTreeTimelineMarkerKind.Abort"/>, empty otherwise.</summary>
        public readonly Guid RelatedGuid;

        /// <summary>What to show on hover: the guard's name, or the sub-tree asset's.</summary>
        public readonly string Label;

        /// <summary>Which lane it belongs beside, taken from the segment it interrupted.</summary>
        public readonly int Depth;

        /// <summary>Index into the recording, for linking back.</summary>
        public readonly int EventIndex;

        public BehaviorTreeTimelineMarker(
            BehaviorTreeTimelineMarkerKind kind,
            int tick,
            int callSiteId,
            Guid nodeGuid,
            Guid relatedGuid,
            string label,
            int depth,
            int eventIndex)
        {
            Kind = kind;
            Tick = tick;
            CallSiteId = callSiteId;
            NodeGuid = nodeGuid;
            RelatedGuid = relatedGuid;
            Label = label;
            Depth = depth;
            EventIndex = eventIndex;
        }
    }
}

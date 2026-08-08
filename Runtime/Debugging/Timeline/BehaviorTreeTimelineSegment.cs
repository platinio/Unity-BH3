using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One stretch of ticks during which a node was active: the coloured bar the scrubber draws.
    ///
    /// <para>
    /// Addressed as <c>(ScopeId, NodeGuid)</c> rather than by guid alone, for the reason the whole debugger
    /// is: a sub-tree asset is instantiated per call site and the clone keeps the original guids, so the same
    /// guid appears once per call site and a guid-only address shows one Attack in two states at once.
    /// </para>
    /// </summary>
    public readonly struct BehaviorTreeTimelineSegment
    {
        /// <summary>An <see cref="ExitTick"/> meaning the node was still running when the recording ended.</summary>
        public const int StillOpen = -1;

        public readonly int ScopeId;

        public readonly Guid NodeGuid;

        /// <summary>The node's name when a topology was supplied, otherwise a short form of the guid.</summary>
        public readonly string Name;

        public readonly int EnterTick;

        /// <summary>The tick it stopped, or <see cref="StillOpen"/>.</summary>
        public readonly int ExitTick;

        /// <summary>
        /// How it ended. <see cref="BehaviorTreeOutcome.Running"/> while <see cref="ExitTick"/> is
        /// <see cref="StillOpen"/>.
        /// </summary>
        public readonly BehaviorTreeOutcome Outcome;

        /// <summary>The guard that aborted it, when <see cref="Outcome"/> is
        /// <see cref="BehaviorTreeOutcome.Aborted"/>. Empty otherwise.</summary>
        public readonly Guid GuardGuid;

        /// <summary>Nesting level at the moment it entered. The lane it is drawn in, before overlap splitting.</summary>
        public readonly int Depth;

        /// <summary>Index into the recording of the event that opened this segment, for linking back.</summary>
        public readonly int EnterIndex;

        /// <summary>Index of the event that closed it, or -1.</summary>
        public readonly int ExitIndex;

        public BehaviorTreeTimelineSegment(
            int scopeId,
            Guid nodeGuid,
            string name,
            int enterTick,
            int exitTick,
            BehaviorTreeOutcome outcome,
            Guid guardGuid,
            int depth,
            int enterIndex,
            int exitIndex)
        {
            ScopeId = scopeId;
            NodeGuid = nodeGuid;
            Name = name;
            EnterTick = enterTick;
            ExitTick = exitTick;
            Outcome = outcome;
            GuardGuid = guardGuid;
            Depth = depth;
            EnterIndex = enterIndex;
            ExitIndex = exitIndex;
        }

        public bool IsOpen => ExitTick == StillOpen;

        /// <summary>
        /// The last tick this segment covers, given where the recording ends. An open segment runs to the end.
        /// </summary>
        public int EndTickOr(int recordingTick) => IsOpen ? recordingTick : ExitTick;

        /// <summary>
        /// Whether the node was active at <paramref name="tick"/>. Inclusive of the enter tick and of the exit
        /// tick: a node that entered and aborted on the same tick still happened, and a zero-width bar the
        /// scrubber cannot land on is a bug report nobody can reproduce.
        /// </summary>
        public bool Covers(int tick, int recordingTick)
        {
            return tick >= EnterTick && tick <= EndTickOr(recordingTick);
        }
    }
}

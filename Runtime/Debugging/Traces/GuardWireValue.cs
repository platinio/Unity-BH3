using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One wire inside a Visual Scripting guard graph, and what was on it when the guard last changed its
    /// mind.
    ///
    /// <para>
    /// Addressed by guid rather than by position, and the guids are the ones <c>FlowGraphDump</c> and
    /// <c>bt_describe_script_graph</c> emit — so a snapshot can be lined up against the asset it came from
    /// without the scene, and an authoring agent can read a snapshot of a graph it generated.
    /// </para>
    /// </summary>
    public readonly struct GuardWireValue
    {
        public readonly Guid ConnectionGuid;

        public readonly Guid SourceUnitGuid;

        public readonly string SourceKey;

        public readonly Guid DestinationUnitGuid;

        public readonly string DestinationKey;

        /// <summary>The value, stringified and capped the same way a recorded variable write is.</summary>
        public readonly string Value;

        /// <summary>
        /// Whether the flow actually pushed a value down this wire. False is not a gap in the recording —
        /// it means the branch was never evaluated, which is itself an answer to why a guard came out the way
        /// it did.
        /// </summary>
        public readonly bool WasEvaluated;

        public GuardWireValue(
            Guid connectionGuid,
            Guid sourceUnitGuid,
            string sourceKey,
            Guid destinationUnitGuid,
            string destinationKey,
            string value,
            bool wasEvaluated)
        {
            ConnectionGuid = connectionGuid;
            SourceUnitGuid = sourceUnitGuid;
            SourceKey = sourceKey;
            DestinationUnitGuid = destinationUnitGuid;
            DestinationKey = destinationKey;
            Value = value;
            WasEvaluated = wasEvaluated;
        }

        /// <summary>What to show on the wire.</summary>
        public string Label => WasEvaluated ? Value : "(not evaluated)";
    }
}

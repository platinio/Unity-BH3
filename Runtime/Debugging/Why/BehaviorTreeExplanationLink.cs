using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>What a clause points at, so the UI can make it clickable without parsing the sentence.</summary>
    public enum BehaviorTreeLinkKind : byte
    {
        None = 0,

        /// <summary>A node on the canvas. Selecting it is the useful action.</summary>
        Node,

        /// <summary>A guard. Also a node on the canvas, but worth distinguishing so the UI can style it.</summary>
        Guard,

        /// <summary>A moment. The scrubber's job once Component 2 exists; until then, context.</summary>
        Tick,

        /// <summary>A variable, by key. The variable watch's job once Component 4 exists.</summary>
        Variable,
    }

    /// <summary>
    /// Where a clause points.
    ///
    /// <para>
    /// Structured rather than embedded in the text because the same explanation is read by a UI panel that
    /// wants click targets, by a CLI that wants plain sentences, and by an authoring agent that wants the
    /// guids. Formatting a guid into a string and re-extracting it downstream is how a tool ends up with
    /// three subtly different parsers.
    /// </para>
    /// </summary>
    public readonly struct BehaviorTreeExplanationLink
    {
        public static readonly BehaviorTreeExplanationLink None = default;

        public readonly BehaviorTreeLinkKind Kind;

        /// <summary>The node or guard, when <see cref="Kind"/> names one.</summary>
        public readonly Guid NodeGuid;

        /// <summary>
        /// Which call site the target is in. Always carried with a node guid: a guid alone is ambiguous when
        /// two call sites run the same shared branch.
        /// </summary>
        public readonly int ScopeId;

        /// <summary>The tick the clause is about, or -1.</summary>
        public readonly int Tick;

        /// <summary>Order within the tick, or -1. What the scrubber needs to land on the right event.</summary>
        public readonly int Sequence;

        /// <summary>The variable key, when <see cref="Kind"/> is <see cref="BehaviorTreeLinkKind.Variable"/>.</summary>
        public readonly string VariableKey;

        private BehaviorTreeExplanationLink(BehaviorTreeLinkKind kind, Guid nodeGuid, int scopeId, int tick, int sequence, string variableKey)
        {
            Kind = kind;
            NodeGuid = nodeGuid;
            ScopeId = scopeId;
            Tick = tick;
            Sequence = sequence;
            VariableKey = variableKey;
        }

        public bool HasLink => Kind != BehaviorTreeLinkKind.None;

        public static BehaviorTreeExplanationLink ToNode(Guid guid, int scopeId, int tick = -1, int sequence = -1) =>
            new(BehaviorTreeLinkKind.Node, guid, scopeId, tick, sequence, null);

        public static BehaviorTreeExplanationLink ToGuard(Guid guid, int scopeId, int tick = -1, int sequence = -1) =>
            new(BehaviorTreeLinkKind.Guard, guid, scopeId, tick, sequence, null);

        public static BehaviorTreeExplanationLink ToTick(int tick, int scopeId, int sequence = -1) =>
            new(BehaviorTreeLinkKind.Tick, Guid.Empty, scopeId, tick, sequence, null);

        public static BehaviorTreeExplanationLink ToVariable(string key, int scopeId, int tick = -1, int sequence = -1) =>
            new(BehaviorTreeLinkKind.Variable, Guid.Empty, scopeId, tick, sequence, key);
    }
}

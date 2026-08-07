using System;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One thing that happened, flat. A struct stored in a pre-allocated array, so recording a tick's worth
    /// of events allocates nothing.
    ///
    /// <para>
    /// The fields are a union rather than a hierarchy on purpose. A polymorphic event would mean one
    /// allocation per event and a garbage spike proportional to how interesting the frame was, which is
    /// exactly backwards for a tool you leave on while hunting an intermittent bug.
    /// </para>
    /// </summary>
    public readonly struct BehaviorTreeEvent
    {
        /// <summary>How much of a value is kept. Long enough to identify it, short enough to bound the buffer.</summary>
        public const int MaxValueLength = 64;

        public readonly BehaviorTreeEventKind Kind;

        /// <summary>Machine ticks since this agent's tree started. The scrubber's axis.</summary>
        public readonly int Tick;

        /// <summary>
        /// Order within the tick. A tick contains many events and their order is the information — "the guard
        /// flipped before the node aborted" is the entire causal claim. Frame number cannot express it and
        /// wall-clock time is too coarse.
        /// </summary>
        public readonly int Sequence;

        /// <summary><see cref="UnityEngine.Time.frameCount"/>, for lining two agents' recordings up.</summary>
        public readonly int Frame;

        /// <summary><see cref="UnityEngine.Time.time"/>, for showing a designer when something happened.</summary>
        public readonly float Time;

        /// <summary>
        /// Which call site this happened in. A node guid alone is not unique within an agent: a sub-tree
        /// asset is instantiated per <see cref="RunBehaviorTreeGraphNode"/> and the clone keeps the original
        /// guids, so the same guid appears once per call site. The variable scope chain is already one scope
        /// per call site, so the recorder numbers those and pairs the id with the guid.
        /// </summary>
        public readonly int ScopeId;

        /// <summary>The node this is about.</summary>
        public readonly Guid NodeGuid;

        /// <summary>
        /// The other party, by kind: the guard for <see cref="BehaviorTreeEventKind.NodeAborted"/> and
        /// <see cref="BehaviorTreeEventKind.NodeSkipped"/>, the writing node for
        /// <see cref="BehaviorTreeEventKind.VariableWrite"/>, and <see cref="Guid.Empty"/> otherwise.
        /// </summary>
        public readonly Guid RelatedGuid;

        /// <summary>The status on <see cref="BehaviorTreeEventKind.NodeExit"/>.</summary>
        public readonly ExecutionStatus Status;

        /// <summary>The new result on <see cref="BehaviorTreeEventKind.GuardEval"/>.</summary>
        public readonly bool Flag;

        /// <summary>Variable name, or the sub-tree asset name for a push/pop. Null otherwise.</summary>
        public readonly string Key;

        /// <summary>Previous value, already stringified and capped. Null except on a write.</summary>
        public readonly string OldValue;

        /// <summary>New value, already stringified and capped. Null except on a write.</summary>
        public readonly string NewValue;

        /// <summary>
        /// Who wrote it, when the writer is not a node in the tree — a perception sensor, or anything else
        /// outside the graph that publishes agent state. Null for a write made by a node, where
        /// <see cref="RelatedGuid"/> names it precisely.
        /// <para>
        /// This exists because the facts a tree reads are mostly produced by always-on sensors rather than
        /// by branches, so "which node wrote hasTarget" has no answer for the most common and most confusing
        /// case. A name is weaker than a guid and that is the honest trade: it is what an out-of-graph
        /// writer can offer.
        /// </para>
        /// </summary>
        public readonly string Writer;

        private BehaviorTreeEvent(
            BehaviorTreeEventKind kind,
            int tick,
            int sequence,
            int frame,
            float time,
            int scopeId,
            Guid nodeGuid,
            Guid relatedGuid,
            ExecutionStatus status,
            bool flag,
            string key,
            string oldValue,
            string newValue,
            string writer)
        {
            Kind = kind;
            Tick = tick;
            Sequence = sequence;
            Frame = frame;
            Time = time;
            ScopeId = scopeId;
            NodeGuid = nodeGuid;
            RelatedGuid = relatedGuid;
            Status = status;
            Flag = flag;
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            Writer = writer;
        }

        /// <summary>
        /// Builds an event. The recorder stamps tick/sequence/frame/time, so callers only describe what
        /// happened — one place decides what "when" means.
        /// <para>
        /// Public because reading a recording back is as much a use as writing one: an importer rebuilding a
        /// scrubber from exported JSON constructs these, and so does a test that needs a known buffer.
        /// </para>
        /// </summary>
        public static BehaviorTreeEvent Create(
            BehaviorTreeEventKind kind,
            int tick,
            int sequence,
            int frame,
            float time,
            int scopeId,
            Guid nodeGuid,
            Guid relatedGuid = default,
            ExecutionStatus status = ExecutionStatus.None,
            bool flag = false,
            string key = null,
            string oldValue = null,
            string newValue = null,
            string writer = null)
        {
            return new BehaviorTreeEvent(
                kind, tick, sequence, frame, time, scopeId,
                nodeGuid, relatedGuid, status, flag, key, oldValue, newValue, writer);
        }

        /// <summary>
        /// A value as the recording keeps it: short, never null, and never a live reference.
        /// <para>
        /// Stringified at record time rather than at export, against the general rule of not formatting on
        /// the hot path, because the alternative is worse in both directions. Holding the <c>object</c> would
        /// box every struct — an allocation on the very path that must not allocate — and would let a
        /// reference type keep mutating after the event was recorded, so the recording would show the
        /// value's state at export rather than at write. Only <see cref="BehaviorTreeEventKind.VariableWrite"/>
        /// pays this, and a variable that changes every tick is a bug the recorder exists to surface.
        /// </para>
        /// </summary>
        public static string Describe(object value)
        {
            if (value == null) return "null";

            // ToString on a destroyed UnityEngine.Object is safe and reads "null", which is what we want to
            // record; on a live one it is the name plus type, which is what a designer recognises.
            var text = value.ToString();
            if (string.IsNullOrEmpty(text)) return "\"\"";

            return text.Length <= MaxValueLength ? text : text.Substring(0, MaxValueLength) + "…";
        }
    }
}

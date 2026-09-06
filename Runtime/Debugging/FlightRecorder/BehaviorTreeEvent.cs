using System;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

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
    ///
    /// <para>
    /// The cost of that choice is that several fields mean different things depending on
    /// <see cref="Kind"/>, so <b>read this table before reading the fields</b> — the per-field summaries
    /// below cannot tell you what a field holds without knowing the kind, and one row genuinely inverts the
    /// usual roles.
    /// </para>
    ///
    /// <list type="table">
    ///   <listheader>
    ///     <term>Kind</term><description>NodeGuid / RelatedGuid / Key / other</description>
    ///   </listheader>
    ///   <item>
    ///     <term><see cref="BehaviorTreeEventKind.NodeEnter"/></term>
    ///     <description>the node that started / empty / null.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="BehaviorTreeEventKind.NodeExit"/></term>
    ///     <description>the node that stopped / empty / null. <see cref="Status"/> is what it ended on.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="BehaviorTreeEventKind.NodeAborted"/></term>
    ///     <description>the node killed mid-run / the guard that killed it / null.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="BehaviorTreeEventKind.NodeSkipped"/></term>
    ///     <description>the node refused entry / the guard that refused it / null.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="BehaviorTreeEventKind.GuardEval"/></term>
    ///     <description><b>inverted:</b> the <i>guard</i> / the node it protects / null.
    ///     <see cref="Flag"/> is the guard's new answer. This is the one kind whose subject is not a
    ///     behaviour node, because the event is about the guard changing its mind.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="BehaviorTreeEventKind.VariableWrite"/></term>
    ///     <description>empty / the writing node, or empty when the writer was outside the tree / the
    ///     <b>variable name</b>. <see cref="OldValue"/> and <see cref="NewValue"/> are that variable's
    ///     values, <see cref="VariableKind"/> is which store it landed in, and <see cref="Writer"/> names an
    ///     out-of-tree writer.</description>
    ///   </item>
    ///   <item>
    ///     <term><see cref="BehaviorTreeEventKind.TreePushed"/>, <see cref="BehaviorTreeEventKind.TreePopped"/></term>
    ///     <description>the <c>RunBehaviorTreeGraphNode</c> / empty / the <b>sub-tree asset name</b>, not a
    ///     variable name.</description>
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// <see cref="CallSiteId"/>, <see cref="Tick"/>, <see cref="Sequence"/>, <see cref="Frame"/> and
    /// <see cref="Time"/> mean the same thing on every kind.
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
        /// Which <b>call site</b> — which running instance of a tree — this happened in. Not which node:
        /// a whole branch shares one call site, and the nodes inside it are told apart by
        /// <see cref="NodeGuid"/>. Think stack frame, not instruction pointer.
        /// <para>
        /// Needed because a node guid alone is not unique within one agent. A sub-tree asset is instantiated
        /// per <see cref="RunBehaviorTreeGraphNode"/> and <c>guid</c> is serialized, so the clone keeps the
        /// original guids and the same guid appears once per call site. The variable scope chain is already
        /// exactly one scope per call site, so the recorder numbers those; see
        /// <see cref="BehaviorTreeCallSite"/> for the table this indexes into.
        /// </para>
        /// </summary>
        public readonly int CallSiteId;

        /// <summary>
        /// The subject. Which node that is depends on <see cref="Kind"/> — see the table on this type, and
        /// note that <see cref="BehaviorTreeEventKind.GuardEval"/> puts the guard here rather than a
        /// behaviour node.
        /// </summary>
        public readonly Guid NodeGuid;

        /// <summary>
        /// The other party, or <see cref="Guid.Empty"/> where the kind has none. Which party depends on
        /// <see cref="Kind"/> — see the table on this type.
        /// </summary>
        public readonly Guid RelatedGuid;

        /// <summary>
        /// What a node ended on. Only meaningful for <see cref="BehaviorTreeEventKind.NodeExit"/>;
        /// <see cref="ExecutionStatus.None"/> elsewhere.
        /// </summary>
        public readonly ExecutionStatus Status;

        /// <summary>
        /// A guard's new answer. Only meaningful for <see cref="BehaviorTreeEventKind.GuardEval"/>, where the
        /// guard is <see cref="NodeGuid"/> and the node it protects is <see cref="RelatedGuid"/>.
        /// </summary>
        public readonly bool Flag;

        /// <summary>
        /// A name whose meaning depends on <see cref="Kind"/>: the <b>variable name</b> on
        /// <see cref="BehaviorTreeEventKind.VariableWrite"/>, the <b>sub-tree asset name</b> on
        /// <see cref="BehaviorTreeEventKind.TreePushed"/> and <see cref="BehaviorTreeEventKind.TreePopped"/>,
        /// null on every other kind. Two unrelated meanings sharing a slot is the price of the flat struct.
        /// </summary>
        public readonly string Key;

        /// <summary>
        /// The value <see cref="Key"/> held <i>before</i> the write, stringified and capped. Only meaningful
        /// on <see cref="BehaviorTreeEventKind.VariableWrite"/>; null elsewhere. "null" here means the
        /// variable was not declared yet, which is a real answer rather than an error.
        /// </summary>
        public readonly string OldValue;

        /// <summary>
        /// The value <see cref="Key"/> was set to, stringified and capped. Only meaningful on
        /// <see cref="BehaviorTreeEventKind.VariableWrite"/>; null elsewhere.
        /// </summary>
        public readonly string NewValue;

        /// <summary>
        /// Which store the write landed in, and therefore whose value it is:
        /// <see cref="BehaviorTreeVariableKind.Graph"/> is scratch belonging to the one branch instance
        /// named by <see cref="CallSiteId"/>, while <see cref="BehaviorTreeVariableKind.Object"/> is agent
        /// state every branch can see.
        /// <para>
        /// Recorded because <see cref="CallSiteId"/> cannot stand in for it. That is where the write was made
        /// <i>from</i>, not where the value lives — a node inside Combat writing agent state would otherwise
        /// be filed under Combat, and a variable watch built on that would show agent-wide facts as branch
        /// scratch.
        /// </para>
        /// <para>
        /// <see cref="BehaviorTreeVariableKind.None"/> on every kind that is not a write, meaning "not
        /// applicable". No store answers to that name, so it cannot collide with a real recorded write —
        /// which is why the enum carries the value rather than the recorder borrowing one, as it did when
        /// this field was a <c>Unity.VisualScripting.VariableKind</c> and "not a write" was spelled
        /// <c>Flow</c>.
        /// </para>
        /// </summary>
        public readonly BehaviorTreeVariableKind VariableKind;

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
            int callSiteId,
            Guid nodeGuid,
            Guid relatedGuid,
            ExecutionStatus status,
            bool flag,
            string key,
            string oldValue,
            string newValue,
            BehaviorTreeVariableKind variableKind,
            string writer)
        {
            Kind = kind;
            Tick = tick;
            Sequence = sequence;
            Frame = frame;
            Time = time;
            CallSiteId = callSiteId;
            NodeGuid = nodeGuid;
            RelatedGuid = relatedGuid;
            Status = status;
            Flag = flag;
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
            VariableKind = variableKind;
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
            int callSiteId,
            Guid nodeGuid,
            Guid relatedGuid = default,
            ExecutionStatus status = ExecutionStatus.None,
            bool flag = false,
            string key = null,
            string oldValue = null,
            string newValue = null,
            BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.None,
            string writer = null)
        {
            return new BehaviorTreeEvent(
                kind, tick, sequence, frame, time, callSiteId,
                nodeGuid, relatedGuid, status, flag, key, oldValue, newValue, variableKind, writer);
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

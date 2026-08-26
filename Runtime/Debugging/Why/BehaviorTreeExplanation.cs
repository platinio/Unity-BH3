using System;
using System.Collections.Generic;
using System.Text;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>How a node's most recent episode ended, which is what the first sentence is about.</summary>
    public enum BehaviorTreeOutcome : byte
    {
        /// <summary>Nothing about this node is in the buffer.</summary>
        NoRecord = 0,

        /// <summary>Entered and had not exited by the tick being asked about.</summary>
        Running,

        Succeeded,

        Failed,

        /// <summary>A guard turned false while it was running and killed the branch under it.</summary>
        Aborted,

        /// <summary>A guard was false as it was about to start, so it never entered.</summary>
        Skipped,

        /// <summary>
        /// A higher-priority sibling became able to run and took its slot. Deliberately not
        /// <see cref="Aborted"/>: nothing under this node turned false — something better outbid it — and the
        /// two send whoever is asking to different places.
        /// </summary>
        TakenOver,
    }

    /// <summary>
    /// Why a node did what it did, as a structure rather than a paragraph.
    ///
    /// <para>
    /// Assembled from recorded events, never inferred from the tree's current state — the whole value of this
    /// over reading the canvas is that it reports what actually happened on a tick that has already passed.
    /// Where a claim cannot be justified from the buffer it is not made; where it can only be correlated it is
    /// marked <see cref="BehaviorTreeClauseRole.Caveat"/> and worded as correlation.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeExplanation
    {
        private readonly List<BehaviorTreeExplanationClause> clauses;

        public BehaviorTreeExplanation(
            Guid nodeGuid,
            int callSiteId,
            string subjectName,
            string callSitePath,
            int atTick,
            BehaviorTreeOutcome outcome,
            string headline,
            List<BehaviorTreeExplanationClause> clauses,
            GuardTrace trace = null)
        {
            Trace = trace;
            NodeGuid = nodeGuid;
            CallSiteId = callSiteId;
            SubjectName = subjectName;
            CallSitePath = callSitePath;
            AtTick = atTick;
            Outcome = outcome;
            Headline = headline;
            this.clauses = clauses ?? new List<BehaviorTreeExplanationClause>();
        }

        /// <summary>The node this is about.</summary>
        public Guid NodeGuid { get; }

        /// <summary>Which call site it is about — a guid alone does not identify a node within one agent.</summary>
        public int CallSiteId { get; }

        /// <summary>What to call the node: its name when a topology was supplied, its short guid otherwise.</summary>
        public string SubjectName { get; }

        /// <summary>Where it lives, e.g. <c>Zombie -> Combat -> Attack</c>.</summary>
        public string CallSitePath { get; }

        /// <summary>The tick this explanation is written from.</summary>
        public int AtTick { get; }

        public BehaviorTreeOutcome Outcome { get; }

        /// <summary>The one-line answer.</summary>
        public string Headline { get; }

        public IReadOnlyList<BehaviorTreeExplanationClause> Clauses => clauses;

        /// <summary>
        /// What the guard was looking at when it changed its mind, or null when no trace was captured.
        ///
        /// <para>
        /// Structured rather than folded into a clause because the chain is a tree whose every node is worth
        /// pointing at — the panel makes each one selectable and offers a snapshot where one exists, and a
        /// consumer that only wants a sentence can call <see cref="GuardTrace.Describe"/>. Formatting it into
        /// text here would force every other reader to parse it back out.
        /// </para>
        /// </summary>
        public GuardTrace Trace { get; }

        /// <summary>The whole thing as text, for a CLI, a log, or a test assertion.</summary>
        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append(SubjectName);

            if (!string.IsNullOrEmpty(CallSitePath)) text.Append(" (").Append(CallSitePath).Append(')');

            text.Append(": ").Append(Headline);

            foreach (var clause in clauses)
            {
                text.Append('\n').Append("  - ").Append(clause.Text);
            }

            return text.ToString();
        }

        /// <summary>The explanation plus the guard chain indented under it, for a log or a CLI.</summary>
        public string ToStringWithTrace()
        {
            if (Trace == null || Trace.Chain.Count <= 1) return ToString();

            var text = new StringBuilder(ToString());

            // From index 1: the guard itself is the subject of the sentence above, and repeating it here
            // says nothing while inviting a contradiction — the chain records the guard's own node name,
            // whereas the sentence calls it by the value it reads.
            for (int i = 1; i < Trace.Chain.Count; i++)
            {
                var node = Trace.Chain[i];

                text.Append('\n').Append("    ");
                text.Append(' ', (node.Depth - 1) * 2);
                text.Append(node.Name).Append(" -> ").Append(node.Value);
            }

            return text.ToString();
        }
    }
}

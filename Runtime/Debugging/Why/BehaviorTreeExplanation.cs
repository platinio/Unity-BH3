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
            int scopeId,
            string subjectName,
            string callSitePath,
            int atTick,
            BehaviorTreeOutcome outcome,
            string headline,
            List<BehaviorTreeExplanationClause> clauses)
        {
            NodeGuid = nodeGuid;
            ScopeId = scopeId;
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
        public int ScopeId { get; }

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
    }
}

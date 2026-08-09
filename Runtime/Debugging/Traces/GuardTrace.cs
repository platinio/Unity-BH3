using System;
using System.Collections.Generic;
using System.Text;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Why a guard came out the way it did, captured at the instant it changed its mind.
    ///
    /// <para>
    /// The recording already said <em>that</em> a guard flipped and which node died for it. This says what
    /// the guard was looking at — the chain of values that produced the result, and the interior of any
    /// Visual Scripting graph in that chain. Most branches in a real tree are guarded by a script graph, so
    /// without this the most common answer the why-inspector can give ends at "it returned false", which is
    /// the question restated rather than answered.
    /// </para>
    ///
    /// <para>
    /// Captured on transitions only. Guards evaluate every tick and a trace is far heavier than an event, so
    /// steady state stays free; the moments that cost anything are exactly the ones worth keeping. Traces
    /// live in their own ring for the same reason — they are variable-sized, and the flat event struct is
    /// flat on purpose.
    /// </para>
    /// </summary>
    public sealed class GuardTrace
    {
        private readonly List<GuardTraceNode> chain;
        private readonly List<GuardGraphSnapshot> snapshots;

        public GuardTrace(
            int tick,
            int sequence,
            int callSiteId,
            Guid guardGuid,
            Guid ownerGuid,
            bool result,
            List<GuardTraceNode> chain,
            List<GuardGraphSnapshot> snapshots)
        {
            Tick = tick;
            Sequence = sequence;
            CallSiteId = callSiteId;
            GuardGuid = guardGuid;
            OwnerGuid = ownerGuid;
            Result = result;
            this.chain = chain ?? new List<GuardTraceNode>();
            this.snapshots = snapshots ?? new List<GuardGraphSnapshot>();
        }

        /// <summary>
        /// The tick and sequence of the <c>GuardEval</c> this belongs to. Sequence as well as tick because a
        /// tick holds many events and their order is the causal claim — the same reason every event carries
        /// one.
        /// </summary>
        public int Tick { get; }

        public int Sequence { get; }

        public int CallSiteId { get; }

        public Guid GuardGuid { get; }

        /// <summary>The node this guard protects.</summary>
        public Guid OwnerGuid { get; }

        public bool Result { get; }

        /// <summary>Depth-first, guard first. Empty when the walk found nothing connected.</summary>
        public IReadOnlyList<GuardTraceNode> Chain => chain;

        public IReadOnlyList<GuardGraphSnapshot> Snapshots => snapshots;

        public bool Matches(int tick, int sequence) => Tick == tick && Sequence == sequence;

        /// <summary>
        /// The chain on one line, for a sentence, a log, or a CLI: <c>Not -> false &lt;- hasTarget -> true</c>.
        /// The panel renders the tree properly; this is what makes the same answer readable without one.
        /// </summary>
        public string Describe()
        {
            if (chain.Count == 0) return string.Empty;

            var text = new StringBuilder();

            // Skipping the guard itself: the sentence around this already named it and said what it returned,
            // so repeating it here would push the part the reader does not know off the end of the line.
            for (int i = 1; i < chain.Count; i++)
            {
                if (text.Length > 0) text.Append(" <- ");

                text.Append(chain[i].Name).Append(" -> ").Append(chain[i].Value);
            }

            return text.ToString();
        }
    }
}

using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A recording held as plain data: what an import produces, and what a test writes by hand.
    ///
    /// <para>
    /// Deliberately not a ring. A ring exists to bound memory while events are arriving; a recording that has
    /// stopped arriving has a known size, and pretending otherwise would mean an imported recording could
    /// drop events the file actually contained. <see cref="Dropped"/> is carried across from the source
    /// instead, so a clipped recording stays honestly clipped after a round trip.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeRecordingSnapshot : IBehaviorTreeRecording
    {
        private readonly List<BehaviorTreeEvent> events;
        private readonly List<BehaviorTreeCallSite> callSites;

        private readonly List<GuardTrace> traces;

        public BehaviorTreeRecordingSnapshot(
            string agentName,
            string treeName,
            int tick,
            IEnumerable<BehaviorTreeEvent> events = null,
            IEnumerable<BehaviorTreeCallSite> callSites = null,
            int dropped = 0,
            IEnumerable<GuardTrace> traces = null)
        {
            AgentName = agentName;
            TreeName = treeName;
            Tick = tick;
            Dropped = dropped;

            this.traces = traces != null ? new List<GuardTrace>(traces) : new List<GuardTrace>();

            this.events = events != null ? new List<BehaviorTreeEvent>(events) : new List<BehaviorTreeEvent>();
            this.callSites = callSites != null ? new List<BehaviorTreeCallSite>(callSites) : new List<BehaviorTreeCallSite>();

            // A recording always has a root call site, even one built from a file that omitted it. Every event
            // carries a call-site id and the explainer resolves it to a name; an id with no row would read as
            // "(unknown)" throughout an otherwise fine recording.
            if (this.callSites.Count == 0)
            {
                this.callSites.Add(new BehaviorTreeCallSite(
                    BehaviorTreeCallSite.RootId, BehaviorTreeCallSite.RootId, System.Guid.Empty, treeName));
            }
        }

        public string AgentName { get; }

        public string TreeName { get; }

        public int Tick { get; }

        public int Dropped { get; }

        public int EventCount => events.Count;

        public BehaviorTreeEvent EventAt(int index) => events[index];

        public IReadOnlyList<BehaviorTreeCallSite> CallSites => callSites;

        public IReadOnlyList<GuardTrace> Traces => traces;

        public GuardTrace TraceFor(int tick, int sequence)
        {
            // Newest first: an explanation is almost always about something that just happened, and a guard
            // that flips often has several traces that differ only in when.
            for (int i = traces.Count - 1; i >= 0; i--)
            {
                if (traces[i] != null && traces[i].Matches(tick, sequence)) return traces[i];
            }

            return null;
        }

        /// <summary>Copies a live recorder, so a recording can be frozen without stopping the agent.</summary>
        public static BehaviorTreeRecordingSnapshot From(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null) return new BehaviorTreeRecordingSnapshot("(no agent)", "(no tree)", 0);

            return new BehaviorTreeRecordingSnapshot(
                recorder.AgentName,
                recorder.TreeName,
                recorder.Tick,
                recorder.Events,
                recorder.CallSites,
                recorder.Events.Dropped,
                recorder.Traces);
        }
    }
}

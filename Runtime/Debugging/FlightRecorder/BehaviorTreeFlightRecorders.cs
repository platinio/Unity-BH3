using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Every recorder currently running, and the switch that turns the lot off.
    ///
    /// <para>
    /// The registry exists so a tool can ask "what is every agent doing" without walking the scene for
    /// <see cref="BehaviorTreeMachine"/> components — the multi-agent triage view is a list of these, and
    /// the whole point of that view is to work when there are two hundred of them.
    /// </para>
    /// </summary>
    public static class BehaviorTreeFlightRecorders
    {
        private static readonly List<BehaviorTreeFlightRecorder> active = new();

        /// <summary>
        /// The kill switch. Off means every recorder drops events, which is the answer to "I have two hundred
        /// agents and I only care about one" and to "I am profiling, stop touching my frame".
        /// </summary>
        public static bool GloballyEnabled { get; set; } = true;

        /// <summary>
        /// Capacity handed to recorders created from here on. Lowering it does not shrink existing ones —
        /// a ring is allocated once and never resized, which is what keeps it allocation-free in use.
        /// </summary>
        public static int DefaultCapacity { get; set; } = BehaviorTreeFlightRecorder.DefaultCapacity;

        /// <summary>
        /// The kill switch for guard traces specifically. Separate from
        /// <see cref="GloballyEnabled"/> because the two cost very different amounts: an event is a struct
        /// written into a pre-allocated slot, a trace re-pulls a chain of ports and walks a script graph on
        /// every guard transition. Turning this off keeps the recording and drops only the explanations of
        /// why guards changed their minds.
        /// </summary>
        public static bool TracingGloballyEnabled { get; set; } = true;

        /// <summary>Trace ring size handed to recorders created from here on.</summary>
        public static int DefaultTraceCapacity { get; set; } = GuardTraceRing.DefaultCapacity;

        public static IReadOnlyList<BehaviorTreeFlightRecorder> Active => active;

        public static void Register(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null || active.Contains(recorder)) return;

            active.Add(recorder);
        }

        public static void Unregister(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null) return;

            active.Remove(recorder);

            // Breakpoint tallies are counted per agent and keyed by the recording, so they go when the
            // recording does. Otherwise a destroyed agent's ring stays alive as a dictionary key for the rest
            // of the session, and its count would still be sitting in the panel's total.
            BehaviorTreeBreakpoints.Forget(recorder);
        }

        /// <summary>
        /// Drops every recorder. Domain reload does not run destructors, and a static list that survives play
        /// mode would otherwise accumulate recorders for agents that no longer exist.
        /// </summary>
        public static void Reset()
        {
            foreach (var recorder in active)
            {
                BehaviorTreeBreakpoints.Forget(recorder);
            }

            active.Clear();
        }

        public static void ClearAll()
        {
            foreach (var recorder in active)
            {
                recorder.Clear();
            }
        }
    }
}

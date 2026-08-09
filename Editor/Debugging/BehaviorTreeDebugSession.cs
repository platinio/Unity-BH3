using ArcaneOnyx.BehaviorTree.Debugging;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// What the debugger is currently looking at: which recording, and at which tick.
    ///
    /// <para>
    /// The timeline owns both answers — it is the panel with the playhead and the one that can open a file —
    /// so it publishes them here and every other panel reads them. The alternative, each panel resolving its
    /// own, is what <see cref="BehaviorTreeDebugTarget"/> already exists to prevent for the live agent, and
    /// the failure is worse once a file is involved: the variable watch would list values from a recording
    /// while the ghosted canvas beside it showed a different one, with nothing on screen admitting they were
    /// two different agents.
    /// </para>
    ///
    /// <para>
    /// Deliberately not a resolver. It holds what the timeline drew last and nothing more, so a panel that
    /// opens before the timeline ever has still has to answer for itself — see the fallback in
    /// <see cref="BehaviorTreeVariableWatchPanel"/>. Collapsing the timeline dock freezes these values rather
    /// than clearing them, which is right: the canvas stays ghosted at the same tick, so the watch should too.
    /// </para>
    /// </summary>
    public static class BehaviorTreeDebugSession
    {
        /// <summary>The recording the timeline is showing, live or loaded, or null.</summary>
        public static IBehaviorTreeRecording Recording { get; private set; }

        /// <summary>Where the playhead is: the scrubbed tick, or the newest tick when live.</summary>
        public static int Tick { get; private set; } = -1;

        /// <summary>Whether the timeline is parked in the past rather than following the newest tick.</summary>
        public static bool IsScrubbing { get; private set; }

        /// <summary>
        /// The playhead, but only for the recording it was taken from — otherwise -1.
        ///
        /// <para>
        /// The same guard <see cref="BehaviorTreeScrubOverride.TickFor"/> makes for the canvas, and for the
        /// same reason: a tick number means nothing outside its own recording, so handing tick 400 of a live
        /// agent to a table built from a loaded file would produce a confident account of a moment that never
        /// existed.
        /// </para>
        /// </summary>
        public static int TickFor(IBehaviorTreeRecording recording)
        {
            if (recording == null || Recording == null) return -1;

            return ReferenceEquals(Recording, recording) ? Tick : -1;
        }

        /// <summary>Called by the timeline each time it draws, with whatever it is drawing.</summary>
        public static void Publish(IBehaviorTreeRecording recording, int tick, bool scrubbing)
        {
            Recording = recording;
            Tick = tick;
            IsScrubbing = scrubbing;
        }

        public static void Clear()
        {
            Recording = null;
            Tick = -1;
            IsScrubbing = false;
        }
    }
}

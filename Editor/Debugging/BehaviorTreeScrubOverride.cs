using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// While the scrubber is parked on a tick, this is where the canvas reads node state from instead of from
    /// the live nodes.
    ///
    /// <para>
    /// It is a read-only redirect and never writes to a <see cref="BehaviorTreeNode"/>. Writing history back
    /// into the live tree was the cheap option and it is wrong twice: the agent keeps running while you study
    /// the past, so the values would be overwritten within a frame, and any that survived would sit on the
    /// canvas after the panel closed looking exactly like live ones. The guard-snapshot window learned this
    /// the same way (Finding 16); the rule generalises.
    /// </para>
    ///
    /// <para>
    /// Static because there is one canvas and one thing a designer can be looking at. The alternative — a
    /// scrub state per window threaded through every widget — buys nothing, since two windows showing two
    /// different moments of the same tree is not a thing anyone wants to read.
    /// </para>
    /// </summary>
    public static class BehaviorTreeScrubOverride
    {
        /// <summary>How much of the normal colour survives while ghosting. Low enough to be unmistakable.</summary>
        public const float GhostAlpha = 0.45f;

        private static BehaviorTreeTreeState state;
        private static IBehaviorTreeRecording source;
        private static int callSiteId = -1;

        /// <summary>Whether the canvas is showing history rather than the present.</summary>
        public static bool IsActive => state != null;

        /// <summary>The tick being shown, or -1.</summary>
        public static int Tick => state?.Tick ?? -1;

        /// <summary>Which agent the shown state belongs to, for the banner.</summary>
        public static string AgentName { get; private set; }

        /// <summary>
        /// The scrubbed tick, but only for the recording it was actually taken from — otherwise -1.
        ///
        /// <para>
        /// This is what lets another panel follow the scrubber without being able to misread it. A tick number
        /// means nothing outside its own recording, so handing tick 5 of a live agent to an explanation of a
        /// recording loaded from a file would produce a confident answer about a moment that never existed.
        /// Callers pass the recording they are about to explain and get a tick only if it belongs to it.
        /// </para>
        /// </summary>
        public static int TickFor(IBehaviorTreeRecording recording)
        {
            if (state == null || recording == null) return -1;

            return ReferenceEquals(source, recording) ? state.Tick : -1;
        }

        /// <summary>
        /// Parks the canvas on a reconstructed moment.
        /// <paramref name="callSite"/> is the call site the panel has selected, or -1 to answer for whichever
        /// copy of a shared branch was running.
        /// </summary>
        public static void Set(
            BehaviorTreeTreeState value, string agentName, IBehaviorTreeRecording recording, int callSite = -1)
        {
            state = value;
            source = recording;
            AgentName = agentName;
            callSiteId = callSite;

            RepaintCanvas();
        }

        /// <summary>Hands the canvas back to the live tree.</summary>
        public static void Clear()
        {
            if (state == null) return;

            state = null;
            source = null;
            AgentName = null;
            callSiteId = -1;

            RepaintCanvas();
        }

        /// <summary>
        /// The status the canvas should draw for a node: the historical one while scrubbing, and the node's
        /// own otherwise. Every caller passes the live value so that turning scrubbing off needs no other
        /// change anywhere.
        /// </summary>
        public static ExecutionStatus StatusOf(BehaviorTreeNode node, ExecutionStatus live)
        {
            if (node == null) return live;
            if (state == null) return live;

            return callSiteId >= 0 ? state.StatusOf(callSiteId, node.guid) : state.StatusOf(node.guid);
        }

        /// <summary>Whether a node was running at the shown tick, or is running now when not scrubbing.</summary>
        public static bool IsRunning(BehaviorTreeNode node, bool live)
        {
            if (node == null) return live;
            if (state == null) return live;

            return StatusOf(node, live ? ExecutionStatus.Running : ExecutionStatus.None) == ExecutionStatus.Running;
        }

        /// <summary>
        /// Wraps a draw in the ghost tint while scrubbing. Used by the widgets so historical state cannot be
        /// mistaken for live state at a glance, which is the one way this feature could actively mislead.
        /// </summary>
        public static GUI.Scope Ghost() => new GhostScope();

        private static void RepaintCanvas()
        {
            foreach (var window in GraphWindow.tabs)
            {
                window.Repaint();
            }
        }

        private sealed class GhostScope : GUI.Scope
        {
            private readonly Color previous;

            public GhostScope()
            {
                previous = GUI.color;

                if (!IsActive) return;

                var ghosted = previous;
                ghosted.a *= GhostAlpha;
                GUI.color = ghosted;
            }

            protected override void CloseScope() => GUI.color = previous;
        }
    }
}

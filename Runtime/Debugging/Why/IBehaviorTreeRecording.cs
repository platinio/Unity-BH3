using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A recording the why-inspector can read, whether it is still being written or was loaded from a file.
    ///
    /// <para>
    /// The explainer takes this rather than a <see cref="BehaviorTreeFlightRecorder"/> so that a recording
    /// exported from someone else's playtest explains exactly as well as the one in front of you. That is the
    /// point of exporting at all: a recording plus a tree dump is a bug report that can be read without the
    /// scene, and an answer that only works while the agent is alive is not much of an answer.
    /// </para>
    ///
    /// <para>
    /// Indexed rather than enumerable because the explainer scans backwards from the end far more often than
    /// it walks forwards — "what is the most recent thing that happened to this node" is the question almost
    /// every sentence starts from.
    /// </para>
    /// </summary>
    public interface IBehaviorTreeRecording
    {
        /// <summary>The agent this recording is of.</summary>
        string AgentName { get; }

        /// <summary>The root tree's asset name.</summary>
        string TreeName { get; }

        /// <summary>The last tick covered. The explainer's default vantage point.</summary>
        int Tick { get; }

        int EventCount { get; }

        /// <summary>Oldest first, matching <see cref="BehaviorTreeEventRing"/>.</summary>
        BehaviorTreeEvent EventAt(int index);

        IReadOnlyList<BehaviorTreeCallSite> CallSites { get; }

        /// <summary>
        /// How many events fell off the back. Non-zero means the recording is clipped, which the explainer
        /// says out loud: "no enter was recorded" and "it never entered" are different claims, and only the
        /// second one is safe to make when nothing was dropped.
        /// </summary>
        int Dropped { get; }
    }
}

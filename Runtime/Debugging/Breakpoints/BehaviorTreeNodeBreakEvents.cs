using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Which moments in a node's life should stop the editor. A mask rather than four breakpoints so the panel
    /// shows one row per node, and so arming a second moment on a node you already broke on is an edit rather
    /// than a duplicate.
    ///
    /// <para>
    /// <see cref="Skipped"/> is not in the spec's list, which reads "enters / exits / aborts". It is here
    /// because the recorder already emits it as a distinct event and Finding 3 is explicit that skipped and
    /// aborted are different things worded differently — "this branch never started" is the question a
    /// designer most often wants to stop on, and without the flag it is the one case they cannot.
    /// </para>
    /// </summary>
    [Flags]
    public enum BehaviorTreeNodeBreakEvents
    {
        None = 0,

        /// <summary>The node started.</summary>
        Enter = 1 << 0,

        /// <summary>The node stopped, whatever it ended on.</summary>
        Exit = 1 << 1,

        /// <summary>A guard turned false mid-run and killed the node.</summary>
        Aborted = 1 << 2,

        /// <summary>A guard was false as the node was about to start, so it never ran.</summary>
        Skipped = 1 << 3,

        All = Enter | Exit | Aborted | Skipped,
    }

    /// <summary>
    /// Asking about a mask in the words the right-click menu uses, so the call sites read as what they mean
    /// rather than as bit arithmetic.
    /// </summary>
    public static class BehaviorTreeNodeBreakEventsExtensions
    {
        /// <summary>
        /// Whether this mask names <paramref name="moment"/>.
        ///
        /// <para>
        /// Any-of rather than all-of, which is where it parts company with <see cref="Enum.HasFlag"/>: given
        /// a combination, this answers true when the mask holds either one. That is what the matcher wants —
        /// a breakpoint armed for Enter and Exit fires on each of them, not only on both together.
        /// </para>
        /// </summary>
        public static bool Includes(this BehaviorTreeNodeBreakEvents events, BehaviorTreeNodeBreakEvents moment)
        {
            return (events & moment) != 0;
        }

        /// <summary>
        /// The mask with <paramref name="moment"/> flipped: added when it was absent, removed when it was
        /// already there, and every other moment left as it was.
        /// </summary>
        public static BehaviorTreeNodeBreakEvents Toggle(
            this BehaviorTreeNodeBreakEvents events, BehaviorTreeNodeBreakEvents moment)
        {
            return events ^ moment;
        }
    }
}

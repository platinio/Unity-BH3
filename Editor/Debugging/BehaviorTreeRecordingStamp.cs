using System;
using ArcaneOnyx.BehaviorTree.Debugging;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// How far along a recording is: which one, at which tick, holding how many events, having lost how
    /// many.
    ///
    /// <para>
    /// Every debug panel caches something expensive built from a recording -- a timeline, a watch table, an
    /// explanation -- and each one needs the same answer to "has it moved since I built this?". Hand-written
    /// at each panel that is three copies of one rule, and the rule is only right when every part of it is
    /// present. <see cref="IBehaviorTreeRecording.EventCount"/> alone stops moving the moment the ring wraps,
    /// because it saturates at capacity; the tick alone says nothing about events added within a tick. The
    /// variable watch shipped with the tick missing and silently froze after 2048 events while still
    /// labelling itself live. Naming the rule once is what stops the fourth panel from getting it wrong too.
    /// </para>
    ///
    /// <para>
    /// <see cref="IBehaviorTreeRecording.Dropped"/> is the fourth part, and it is the only one that never
    /// saturates: once the ring is full every further event increments it, where <c>EventCount</c> has
    /// stopped. Without it the stamp still has a blind spot, one precondition further in than the shipped
    /// bug -- a wrapped ring on a machine that has stopped ticking. <c>Tick</c> only advances in
    /// <c>BeginTick</c>, which the machine's <c>Update</c> skips once the tree has finished, while
    /// <c>ExternalVariableWrite</c> exists precisely so sensors can keep writing regardless of tree state.
    /// Wrapped, halted, still being written to: every other part of the stamp is frozen and the watch would
    /// freeze with them, live banner and all. Counting what fell off is what notices.
    /// </para>
    ///
    /// <para>
    /// A panel with extra dependencies -- a selected node, a scrub tick -- compares those alongside a stamp
    /// rather than folding them into it, so this stays "where the recording is" and nothing else.
    /// </para>
    /// </summary>
    public readonly struct BehaviorTreeRecordingStamp : IEquatable<BehaviorTreeRecordingStamp>
    {
        private readonly IBehaviorTreeRecording recording;
        private readonly int tick;
        private readonly int eventCount;
        private readonly int dropped;

        private BehaviorTreeRecordingStamp(
            IBehaviorTreeRecording recording, int tick, int eventCount, int dropped)
        {
            this.recording = recording;
            this.tick = tick;
            this.eventCount = eventCount;
            this.dropped = dropped;
        }

        /// <summary>The recording this was taken from, or null when there was none.</summary>
        public IBehaviorTreeRecording Recording => recording;

        /// <summary>
        /// A stamp no recording can equal, for a cache holding nothing yet. This is <c>default</c>, so a
        /// freshly constructed panel starts invalidated without having to remember to say so.
        /// </summary>
        public static BehaviorTreeRecordingStamp None => default;

        public static BehaviorTreeRecordingStamp Of(IBehaviorTreeRecording recording)
        {
            return recording == null
                ? None
                : new BehaviorTreeRecordingStamp(
                    recording, recording.Tick, recording.EventCount, recording.Dropped);
        }

        /// <summary>
        /// Reference equality on the recording, deliberately: two recordings are not the same history just
        /// because they are the same length, so switching agents has to rebuild even when the numbers line up.
        /// </summary>
        public bool Equals(BehaviorTreeRecordingStamp other)
        {
            return ReferenceEquals(recording, other.recording)
                   && tick == other.tick
                   && eventCount == other.eventCount
                   && dropped == other.dropped;
        }

        public override bool Equals(object obj) => obj is BehaviorTreeRecordingStamp other && Equals(other);

        public override int GetHashCode()
        {
            var hash = recording == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(recording);

            hash = hash * 397 ^ tick;
            hash = hash * 397 ^ eventCount;
            hash = hash * 397 ^ dropped;

            return hash;
        }
    }
}

using System;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One variable, as it stood at the watch's tick.
    ///
    /// <para>
    /// <see cref="Value"/> is the value at that tick and nothing else — not the agent's current value. The
    /// distinction is the whole point: a watch that answered for the present while the canvas is ghosted to
    /// tick 400 would put two different moments side by side and let the reader believe they were one.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeVariableWatchRow
    {
        public BehaviorTreeVariableWatchRow(
            string key, IReadOnlyList<BehaviorTreeVariableWatchWrite> history, int writeCount)
        {
            Key = key;
            History = history ?? Array.Empty<BehaviorTreeVariableWatchWrite>();
            WriteCount = writeCount;
        }

        public string Key { get; }

        /// <summary>
        /// The writes to this variable at or before the watch's tick, <b>most recent first</b> and capped —
        /// see <see cref="BehaviorTreeVariableWatch.DefaultHistoryLimit"/>. Never empty: a row exists because
        /// a write was recorded for it.
        /// </summary>
        public IReadOnlyList<BehaviorTreeVariableWatchWrite> History { get; }

        /// <summary>
        /// How many writes the recording holds for this variable at or before the tick, including any the
        /// history cap dropped. A row showing 4 of 57 writes is a variable churning every tick, which is worth
        /// seeing even though the individual writes are not.
        /// </summary>
        public int WriteCount { get; }

        /// <summary>The most recent write at or before the tick. The row's whole reason to exist.</summary>
        public BehaviorTreeVariableWatchWrite Latest => History[0];

        /// <summary>The value at the watch's tick.</summary>
        public string Value => Latest.NewValue;

        public int LastWriteTick => Latest.Tick;

        /// <summary>True when the cap hid older writes, so the panel can say so rather than imply completeness.</summary>
        public bool HistoryClipped => WriteCount > History.Count;

        public override string ToString() => $"{Key} = {Value} (last written tick {LastWriteTick})";
    }
}

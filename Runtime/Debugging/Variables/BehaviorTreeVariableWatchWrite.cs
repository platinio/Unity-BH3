using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One recorded change to one variable, as the watch reports it.
    ///
    /// <para>
    /// A projection of a <see cref="BehaviorTreeEventKind.VariableWrite"/> event rather than the event itself,
    /// so the panel never has to know which of the flat struct's fields mean something on that kind. The
    /// writer arrives already resolved: a guid when a node wrote it, a name when a sensor did, and the two are
    /// kept apart because only the first is something a canvas can select.
    /// </para>
    /// </summary>
    public readonly struct BehaviorTreeVariableWatchWrite
    {
        public readonly int Tick;

        /// <summary>Order within the tick. Two writes to one key on one tick are told apart by this alone.</summary>
        public readonly int Sequence;

        public readonly string OldValue;

        public readonly string NewValue;

        /// <summary>The node that wrote it, or <see cref="Guid.Empty"/> when the writer was outside the tree.</summary>
        public readonly Guid WriterGuid;

        /// <summary>
        /// What to call the writer: a node's display name when the topology could resolve one, the sensor's
        /// own name for an out-of-tree write, and the guid as a last resort. Never null.
        /// </summary>
        public readonly string WriterName;

        public BehaviorTreeVariableWatchWrite(
            int tick, int sequence, string oldValue, string newValue, Guid writerGuid, string writerName)
        {
            Tick = tick;
            Sequence = sequence;
            OldValue = oldValue;
            NewValue = newValue;
            WriterGuid = writerGuid;
            WriterName = writerName;
        }

        /// <summary>Whether the writer is a node the canvas could select, rather than a bare name.</summary>
        public bool HasLocatableWriter => WriterGuid != Guid.Empty;

        public override string ToString() => $"{OldValue} → {NewValue} @ tick {Tick} by {WriterName}";
    }
}

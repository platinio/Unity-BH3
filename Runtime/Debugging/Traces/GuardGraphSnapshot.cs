using System;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// The interior of one Visual Scripting guard graph at the instant a guard changed its result.
    ///
    /// <para>
    /// Values overlaid on an asset, not a copy of the graph. That is a deliberate limit and the reason the
    /// viewer has to tolerate drift: edit the graph after recording and a snapshot may name wires that no
    /// longer exist. Those render as orphaned rather than failing, because a partially readable snapshot of a
    /// bug that already happened still beats none.
    /// </para>
    /// </summary>
    public sealed class GuardGraphSnapshot
    {
        private readonly List<GuardWireValue> wires;

        public GuardGraphSnapshot(Guid ownerNodeGuid, string graphName, List<GuardWireValue> wires)
        {
            OwnerNodeGuid = ownerNodeGuid;
            GraphName = graphName;
            this.wires = wires ?? new List<GuardWireValue>();
        }

        /// <summary>The behavior tree node that owns this graph, so the viewer can name where it came from.</summary>
        public Guid OwnerNodeGuid { get; }

        public string GraphName { get; }

        public IReadOnlyList<GuardWireValue> Wires => wires;

        /// <summary>
        /// Whether anything was actually captured. Empty means the wire values were not available rather than
        /// that the graph did nothing — in a player build there is no Visual Scripting debug data at all,
        /// because the binding that provides it is installed by the editor assembly.
        /// </summary>
        public bool IsEmpty => wires.Count == 0;
    }
}

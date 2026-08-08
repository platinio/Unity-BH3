using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One node of the behavior-tree-side chain feeding a guard, and the value it produced.
    ///
    /// <para>
    /// This is the half of the answer that nothing else can supply. Behavior tree ports keep no debug data,
    /// so unless something re-pulls them at the moment of the transition, "the guard was false" is where the
    /// account stops — and the reader is left to open the graph and work out which of its inputs did it.
    /// </para>
    ///
    /// <para>
    /// Flattened depth-first with a parent index rather than stored as a tree of objects: the chain is small,
    /// it has to survive a JSON round trip, and a flat list with parents is the shape both a renderer and an
    /// importer want.
    /// </para>
    /// </summary>
    public readonly struct GuardTraceNode
    {
        public readonly Guid NodeGuid;

        /// <summary>What the canvas calls it.</summary>
        public readonly string Name;

        /// <summary>The concrete node type, e.g. <c>Not</c> — what it is, as opposed to what it is called.</summary>
        public readonly string TypeName;

        /// <summary>The value it produced, stringified and capped.</summary>
        public readonly string Value;

        /// <summary>How deep in the chain, for rendering the tree without rebuilding it.</summary>
        public readonly int Depth;

        /// <summary>Index of the node this one feeds, or -1 for the guard itself.</summary>
        public readonly int ParentIndex;

        /// <summary>
        /// Index into the trace's snapshots when this node is backed by a Visual Scripting graph, or -1.
        /// This is what puts a "view snapshot" button on the right row.
        /// </summary>
        public readonly int SnapshotIndex;

        public GuardTraceNode(
            Guid nodeGuid, string name, string typeName, string value, int depth, int parentIndex, int snapshotIndex)
        {
            NodeGuid = nodeGuid;
            Name = name;
            TypeName = typeName;
            Value = value;
            Depth = depth;
            ParentIndex = parentIndex;
            SnapshotIndex = snapshotIndex;
        }

        public bool HasSnapshot => SnapshotIndex >= 0;
    }
}

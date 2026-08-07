using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One place a tree is running from: the root tree itself, or a particular
    /// <see cref="RunBehaviorTreeGraphNode"/> that runs a branch.
    ///
    /// <para>
    /// This is the recording's answer to "which Attack?". A sub-tree asset is instantiated per call site, and
    /// <c>guid</c> is serialized, so the clone keeps the original node guids — two call sites running the same
    /// branch produce events whose node guids are identical. Pairing a guid with the call site it happened in
    /// makes the address unique again, and the parent link turns a flat list of call sites back into the tree
    /// of them, which is what a reader needs to say "Attack, under Combat".
    /// </para>
    /// </summary>
    public readonly struct BehaviorTreeCallSite
    {
        /// <summary>The id the root tree always gets. Its <see cref="ParentId"/> is itself.</summary>
        public const int RootId = 0;

        public readonly int Id;

        /// <summary>The call site that runs this one, or <see cref="RootId"/> for the root.</summary>
        public readonly int ParentId;

        /// <summary>The <see cref="RunBehaviorTreeGraphNode"/> that opened this one, or empty for the root.</summary>
        public readonly Guid RunNodeGuid;

        /// <summary>The asset running here, by name. The root's is the agent's tree.</summary>
        public readonly string AssetName;

        public BehaviorTreeCallSite(int id, int parentId, Guid runNodeGuid, string assetName)
        {
            Id = id;
            ParentId = parentId;
            RunNodeGuid = runNodeGuid;
            AssetName = assetName;
        }

        public bool IsRoot => Id == RootId;
    }
}

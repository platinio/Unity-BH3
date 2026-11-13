using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// A sub-tree asset is cloned per call site and node guids are serialized, so one guid appears once
    /// per call site. Pairing the two is what makes a node address unique again.
    /// </summary>
    public readonly struct BehaviorTreeCallSite
    {
        public const int RootId = 0;

        public readonly int Id;
        public readonly int ParentId;
        public readonly Guid RunNodeGuid;
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

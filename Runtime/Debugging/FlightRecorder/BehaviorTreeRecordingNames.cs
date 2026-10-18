namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public static class BehaviorTreeRecordingNames
    {
        public const string NoAssetAssigned = "(none assigned)";
        public const string Unnamed = "(unnamed)";
        public const string UnnamedAgent = "(agent)";
        public const string UnnamedTree = "(tree)";
        public const string UnnamedExternalWriter = "(external)";
        public const string UnknownNodeType = "(unknown)";

        public static string AssetNameOf(RunBehaviorTreeGraphNode runNode)
        {
            if (runNode == null || runNode.BehaviorTreeGraphAsset == null) return NoAssetAssigned;

            return runNode.BehaviorTreeGraphAsset.name;
        }
    }
}

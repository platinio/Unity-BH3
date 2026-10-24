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
        public const string NoRecorder = "(null)";

        public const string NullValue = "null";
        public const string EmptyStringValue = "\"\"";
        public const string NotEvaluated = "(not evaluated)";

        public static string AgentNameOf(BehaviorTreeMachine machine)
        {
            if (machine == null || machine.gameObject == null) return UnnamedAgent;

            return machine.gameObject.name;
        }

        public static string AssetNameOf(RunBehaviorTreeGraphNode runNode)
        {
            if (runNode == null || runNode.RunningTree == null) return NoAssetAssigned;

            return runNode.RunningTree.name;
        }
    }
}

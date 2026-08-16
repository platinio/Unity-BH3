namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Shared by the two nodes that declare ports from a contract they hold a copy of —
    /// <see cref="RunBehaviorTreeGraphNode"/> for a sub-tree and <see cref="VisualScriptGraphVariable"/> for
    /// a Function.
    ///
    /// <para>
    /// It exists because refreshing either node's contract silently removes any port the contract no longer
    /// declares, and the connection feeding that port goes with it. Both nodes now say what that cost, and
    /// two copies of the sentence would be two things to keep in step.
    /// </para>
    /// </summary>
    internal static class ContractPorts
    {
        /// <summary>
        /// The node feeding a port, quoted, or null when nothing feeds it.
        ///
        /// <para>
        /// Guarded rather than trusting: this runs from an editor action on a node that may not be in a
        /// graph yet, and <c>ValueInput.connection</c> reaches through the owning node to the graph to
        /// answer, so an unparented port would throw rather than report "nothing connected".
        /// </para>
        /// </summary>
        public static string DescribeWhatFeeds(ValueInput port)
        {
            if (port?.behaviorTreeNode?.graph == null) return null;

            var source = port.connection?.source;

            return source?.behaviorTreeNode is BehaviorTreeNode sourceNode ? $"'{sourceNode.NodeName}'" : null;
        }
    }
}

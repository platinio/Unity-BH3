using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The one rule for a variable node's <c>Variable Kind</c> field, shared by the nodes that have one.
    /// </summary>
    /// <remarks>
    /// The sibling of <see cref="VariableKeyPort"/>, and the same bargain: a node with no store chosen has
    /// nowhere to read from or write to, so say it on the canvas before anything runs and name the node if
    /// it runs anyway. <see cref="BehaviorTreeVariableKind.None"/> is the zero value precisely so this can
    /// be the answer — the enum it replaced defaulted to a store the tree could not serve, and spent that
    /// mistake as a null or a log line one frame at a time.
    /// </remarks>
    internal static class VariableKindField
    {
        /// <summary>The store to act on, or a throw that names the node — never a silent no-op.</summary>
        public static BehaviorTreeVariableKind Resolve(BehaviorTreeVariableKind kind, string nodeName)
        {
            if (kind == BehaviorTreeVariableKind.None)
            {
                throw new System.InvalidOperationException(
                    $"'{nodeName}' has no variable store. Pick one in Variable Kind.");
            }

            return kind;
        }

        /// <summary>The canvas-time half of the same rule. Call from the node's own CollectProblems.</summary>
        public static void CollectProblems(BehaviorTreeVariableKind kind, List<NodeProblem> into)
        {
            if (kind != BehaviorTreeVariableKind.None) return;

            into.Add(new NodeProblem(NodeProblemSeverity.Error,
                "No variable store, so this node has nowhere to look.",
                "Pick one in Variable Kind."));
        }
    }
}

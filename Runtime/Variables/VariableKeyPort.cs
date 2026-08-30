using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The one rule for a variable node's <c>Key</c> port, shared by the three nodes that have one.
    /// </summary>
    /// <remarks>
    /// The port declares an empty default so the canvas offers an inline field — a bare port gets no
    /// editor at all, which read as a bug to every designer who met it. What the bare port used to buy,
    /// loudness on a forgotten key, is kept here instead: resolving an empty key throws naming the node,
    /// and <see cref="CollectProblems"/> puts the same message on the canvas before anything runs.
    /// </remarks>
    internal static class VariableKeyPort
    {
        /// <summary>The key to act on, or a throw that names the node — never a silent empty lookup.</summary>
        public static string Resolve(ValueInput key, string nodeName)
        {
            var value = key.GetValue<string>();

            if (string.IsNullOrEmpty(value))
            {
                throw new System.InvalidOperationException(
                    $"'{nodeName}' has no variable key. Type one on the Key port, or connect it.");
            }

            return value;
        }

        /// <summary>The canvas-time half of the same rule. Call from the node's own CollectProblems.</summary>
        public static void CollectProblems(ValueInput key, List<NodeProblem> into)
        {
            // A connected port is a runtime answer; a problem scan must not pull the connection.
            if (key.hasValidConnection) return;

            if (string.IsNullOrEmpty(key.GetValue<string>()))
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No variable key, so this node has nothing to act on.",
                    "Type the key on the Key port, or connect it."));
            }
        }
    }
}

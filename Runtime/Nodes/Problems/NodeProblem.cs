

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>How much a problem should interrupt someone.</summary>
    public enum NodeProblemSeverity
    {
        /// <summary>Worth knowing, but the node will still do something sensible.</summary>
        Warning,

        /// <summary>This node will throw, or will silently do the wrong thing, if it runs.</summary>
        Error
    }

    /// <summary>
    /// One thing wrong with one node, in a form the canvas can draw and a human can act on.
    /// </summary>
    public readonly struct NodeProblem
    {
        public NodeProblem(NodeProblemSeverity severity, string summary, string fix = null)
        {
            Severity = severity;
            Summary = summary;
            Fix = fix;
        }

        public NodeProblemSeverity Severity { get; }

        /// <summary>What is wrong, in one line. Shown on the node.</summary>
        public string Summary { get; }

        /// <summary>
        /// What to do about it, if there is a specific answer. Kept separate from the summary so a reader
        /// can tell the diagnosis from the instruction — the two get run together otherwise, and the
        /// instruction is the half people actually need.
        /// </summary>
        public string Fix { get; }

        public override string ToString() => Fix == null ? Summary : $"{Summary}  →  {Fix}";
    }

}

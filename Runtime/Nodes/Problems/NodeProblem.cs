using System.Collections.Generic;

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

    /// <summary>
    /// A node that can say what is wrong with it.
    ///
    /// <para>
    /// A capability interface rather than a type test, following the rule <c>ConditionalExecution</c> already
    /// states and <see cref="IDeclaresWatchedKeys"/> already follows: everything that walks nodes filters on
    /// capability, never on type. A new node with a new kind of problem implements this and appears on the
    /// canvas without anything in the drawing layer being touched.
    /// </para>
    ///
    /// <para>
    /// Implement it for problems a node can see <b>in itself</b>. Problems that only an outside rule can see
    /// — the kind <c>BehaviorTreeVerification</c> knows about — are contributed by registering a provider
    /// instead, so a lint does not have to become a property of the thing it inspects.
    /// </para>
    ///
    /// <para>
    /// <b>Cost matters here.</b> This is read to draw a canvas, so it is cached against an invalidation
    /// counter rather than called per frame — but an implementation that walks a whole graph will still be
    /// felt on a large tree every time something is edited. Report what the node already knows.
    /// </para>
    /// </summary>
    public interface IReportsProblems
    {
        /// <summary>Adds this node's current problems. Adding nothing means the node is fine.</summary>
        void CollectProblems(List<NodeProblem> into);
    }
}

using ArcaneOnyx.VisualScriptingExtension;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The repair a <see cref="NodeProblem"/> carries: what would fix it, as data.
    ///
    /// <para>
    /// <b>Data, not a delegate — because the two halves of a repair live in different assemblies.</b> The
    /// node knows <em>what</em> is wrong and what would fix it, and says so here. Performing the fix is
    /// editor work — undo recording, dirtying, cache invalidation, a confirmation dialog for a shared
    /// asset — that runtime code cannot express and must not try to. So a repair names itself and carries
    /// what the fix needs, and the editor's <c>NodeProblemRepairs</c> is the one place that knows how to
    /// perform each kind. A closed set: a new repair kind is a new class here and a new case there.
    /// </para>
    ///
    /// <para>
    /// Carried by the problem so that a surface can only offer a repair for a defect it is currently
    /// reporting. The canvas context menus predate this and derive their entries independently; any surface
    /// added after it renders buttons from these and cannot contradict the badge.
    /// </para>
    /// </summary>
    public abstract class NodeProblemRepair
    {
        /// <summary>
        /// The verb as a button reads: "Refresh Ports", "Declare 'hp' on IsHurt". Naming the target in the
        /// label is what lets one node show several of these apart.
        /// </summary>
        public abstract string Label { get; }
    }

    /// <summary>
    /// Rebuild this node's ports from the contract it references. The node performing it is the node
    /// reporting the problem — an <see cref="IRefreshesContractPorts"/> — so the repair carries only the
    /// verb, which differs by node kind ("Refresh Ports" on a Function reader, "Refresh Parameters" on a
    /// sub-tree caller) and has to match the context-menu entry for the same act.
    /// </summary>
    public sealed class RefreshContractPortsRepair : NodeProblemRepair
    {
        public RefreshContractPortsRepair(string label)
        {
            Label = label;
        }

        public override string Label { get; }
    }

    /// <summary>
    /// Write into this guard's key triggers whatever its condition declares and they omit. Only a
    /// <c>ReactiveGuard</c> can perform it, which is also the only node kind that reports the trigger drift
    /// this repairs.
    /// </summary>
    public sealed class RefreshWatchedKeysRepair : NodeProblemRepair
    {
        public override string Label => "Refresh Watched Keys";
    }

    /// <summary>
    /// Declare <see cref="Key"/> on <see cref="Function"/> — the repair for a graph that reads a fact its
    /// Function never declared, which is the one drift that silently stops guards waking. The Function is
    /// shared, so performing this confirms first and counts the other trees reading it; that loudness lives
    /// with the editor's performer, not here.
    /// </summary>
    public sealed class DeclareWatchedKeyRepair : NodeProblemRepair
    {
        public DeclareWatchedKeyRepair(FunctionGraphAsset function, string key)
        {
            Function = function;
            Key = key;
        }

        public FunctionGraphAsset Function { get; }

        public string Key { get; }

        public override string Label =>
            Function != null ? $"Declare '{Key}' on {Function.name}" : $"Declare '{Key}'";
    }
}

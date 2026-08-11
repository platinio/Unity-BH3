using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One store of variables, and the rows in it.
    ///
    /// <para>
    /// Grouping is by <see cref="VariableKind"/> first and by call site only within
    /// <see cref="VariableKind.Graph"/>, because that is where a scope boundary actually is. Agent state is
    /// one store however many branches write to it; branch scratch is a different store per running branch,
    /// which is what stops two copies of a shared sub-tree from appearing to share a counter.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeVariableWatchScope
    {
        public BehaviorTreeVariableWatchScope(
            VariableKind kind, int callSiteId, string label, IReadOnlyList<BehaviorTreeVariableWatchRow> rows)
        {
            Kind = kind;
            CallSiteId = callSiteId;
            Label = label;
            Rows = rows ?? Array.Empty<BehaviorTreeVariableWatchRow>();
        }

        public VariableKind Kind { get; }

        /// <summary>
        /// Which branch instance this is, for <see cref="VariableKind.Graph"/>; -1 for every other kind, whose
        /// store is shared and has no call site.
        /// </summary>
        public int CallSiteId { get; }

        /// <summary>
        /// What to call this store: the branch's asset name for a Graph scope, "agent" for
        /// <see cref="VariableKind.Object"/>, and the store's own name otherwise. Disambiguated with the call
        /// site id when one asset is running at two call sites, since the names would otherwise be identical
        /// and the values are not.
        /// </summary>
        public string Label { get; }

        /// <summary>Sorted by key, so a row does not move as the playhead does.</summary>
        public IReadOnlyList<BehaviorTreeVariableWatchRow> Rows { get; }

        public override string ToString() => $"{Label} ({Kind}) — {Rows.Count} variable(s)";
    }
}

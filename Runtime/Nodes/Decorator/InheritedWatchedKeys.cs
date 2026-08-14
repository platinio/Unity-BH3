using System;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The agent facts a guard's condition depends on, collected by walking back from the guard's inputs.
    ///
    /// <para>
    /// A guard reads nothing itself — <see cref="ConditionalExecution.Evaluate"/> pulls a value off a port —
    /// so what its <see cref="GuardTrigger"/> needs to watch is whatever the <em>condition</em> reads. This is
    /// the walk that answers that, and its answer is what makes the watched-keys declaration on a Function do
    /// anything at all: without it the list is verified against the graph in both directions and consumed by
    /// nobody.
    /// </para>
    ///
    /// <para>
    /// <b>The walk is a DAG, not a chain.</b> Two inputs can share a source, and an authored condition is
    /// routinely several nodes deep — <c>GuardOnVariable</c> alone inserts a <c>Not</c> when the expected
    /// value is false, and And/Or composites are the normal shape. A visited set keeps a shared source from
    /// being collected twice, and the depth cap matches the two walks that already exist
    /// (<c>GuardTraceCapture</c>, <c>BehaviorTreeGraphTopology</c>) rather than inventing a third limit.
    /// </para>
    ///
    /// <para>
    /// <b>Nothing here may throw into the tree.</b> This runs on the guard evaluation path, and a scheduling
    /// aid that can break the thing it schedules is worse than no aid at all. A walk that fails yields no
    /// keys, which degrades a guard to its hand-authored schedule — the behaviour it had before this existed.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Resolution allocates; evaluation must not. Callers resolve <b>once per node instance</b> and keep the
    /// array — see <see cref="ReactiveGuard"/>. That is the same editor-time-invalidation choice this feature
    /// already made for <c>FunctionBindingPlan</c>: a graph cannot change in a player build, and re-walking
    /// per evaluation would reintroduce exactly the per-call cost the trigger economy exists to remove.
    /// </remarks>
    public static class InheritedWatchedKeys
    {
        /// <summary>How far to follow a guard's inputs before giving up. Matches the existing guard walks.</summary>
        private const int MaxDepth = 8;

        /// <summary>
        /// Every key declared by a node reachable backwards from <paramref name="guard"/>'s value inputs,
        /// unioned and deduplicated. Never null; empty when the condition declares nothing, which is the
        /// honest answer rather than a reason to guess a schedule.
        /// </summary>
        public static string[] Resolve(ConditionalExecution guard)
        {
            if (guard == null) return Array.Empty<string>();

            try
            {
                var keys = new List<string>();

                Collect(guard, keys, new HashSet<Guid> { guard.guid }, depth: 1);

                return keys.Count == 0 ? Array.Empty<string>() : keys.ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        private static void Collect(BehaviorTreeNode node, List<string> keys, HashSet<Guid> seen, int depth)
        {
            if (node?.valueInputs == null || depth > MaxDepth) return;

            foreach (var input in node.valueInputs)
            {
                var connection = input?.connection;
                if (connection?.source?.behaviorTreeNode is not BehaviorTreeNode source) continue;

                // A DAG, not a tree: two inputs can share a source, and collecting it twice would only add
                // duplicate work — the key set is a union either way.
                if (!seen.Add(source.guid)) continue;

                if (source is IDeclaresWatchedKeys declarer) Add(keys, declarer.DeclaredWatchedKeys);

                // Kept walking past a declarer on purpose. A condition can combine several sources — a
                // Function AND a variable read — and stopping at the first would silently drop the rest.
                Collect(source, keys, seen, depth + 1);
            }
        }

        private static void Add(List<string> keys, IReadOnlyList<string> declared)
        {
            if (declared == null) return;

            for (int i = 0; i < declared.Count; i++)
            {
                var key = declared[i];
                if (string.IsNullOrWhiteSpace(key)) continue;

                key = key.Trim();
                if (!keys.Contains(key)) keys.Add(key);
            }
        }
    }
}

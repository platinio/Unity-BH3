using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Which guards belong to which node, built once per graph and read every repaint.
    ///
    /// <para>
    /// <b>Why this exists.</b> Answering "the guards of this node" meant walking every element in the graph
    /// and filtering. That is fine once; it is not fine at the rate the canvas asks it. A guard's own
    /// <c>CachePosition</c> asks, so does every transition's, and so do both of
    /// <see cref="BehaviorTreeGraphDrawer"/>'s line-routing call sites and the sub-tree preview's bounds
    /// calculation — each of them per element per repaint, because the widgets invalidate their layout every
    /// frame. On the Soldier sample that is tens of thousands of element visits per repaint to answer a
    /// question about a handful of guards, plus a yield-iterator allocation for each walk.
    /// </para>
    ///
    /// <para>
    /// <b>Ownership cannot drift without a collection change</b>, which is what makes
    /// <see cref="GraphIndex"/>'s staleness rule the right one here: every place that calls
    /// <c>ConditionalExecution.UpdateOwner</c> outside the tests adds the guard to <c>graph.elements</c> in
    /// the same breath, so there is no re-parent this would miss. See <see cref="GraphIndex"/> for the
    /// standing rule that nothing may read an index from inside a <c>CollectionChanged</c> dispatch, which
    /// is what keeps the gap between those two statements safe.
    /// </para>
    ///
    /// <para>
    /// <b>Order is part of the answer, not an accident.</b> The map is filled by walking <c>graph.elements</c>
    /// in order, so a guard's place in its owner's list is graph order — the same order
    /// <see cref="BehaviorTreeNode.GetConditionalIndex"/> numbers them in, which is what lets a guard's place
    /// in the drawn stack match the index everything else quotes.
    /// </para>
    ///
    /// <para>
    /// <b>The lists are handed out concretely on purpose.</b> An <c>IReadOnlyList</c> would be the safer
    /// signature, but <c>foreach</c> over one boxes <c>List</c>'s struct enumerator — an allocation per call,
    /// on the exact per-repaint path this class exists to take allocations off. The returned list belongs to
    /// the index and must not be mutated; that is a weaker guarantee than the type could give, traded
    /// deliberately for the allocation.
    /// </para>
    /// </summary>
    public static class GuardIndex
    {
        private sealed class Index : GraphIndex<Dictionary<BehaviorTreeNode, List<ConditionalExecution>>>
        {
            protected override Dictionary<BehaviorTreeNode, List<ConditionalExecution>> Build(BehaviorTreeGraph graph)
            {
                var index = new Dictionary<BehaviorTreeNode, List<ConditionalExecution>>();

                foreach (var graphElement in graph.elements)
                {
                    if (graphElement is not ConditionalExecution guard) continue;

                    var owner = guard.Owner;

                    // A guard whose owner has gone is dangling; the canvas repair deletes it. Indexing it
                    // under null would only invent a bucket nothing asks for.
                    if (owner == null) continue;

                    if (!index.TryGetValue(owner, out var guards))
                    {
                        guards = new List<ConditionalExecution>();
                        index[owner] = guards;
                    }

                    guards.Add(guard);
                }

                return index;
            }

            public Dictionary<BehaviorTreeNode, List<ConditionalExecution>> Of(BehaviorTreeGraph graph) => For(graph);
        }

        private static readonly Index Instance = new Index();

        /// <summary>Handed back for a node with no guards, so the common case allocates nothing either.</summary>
        private static readonly List<ConditionalExecution> None = new();

        /// <summary>
        /// <paramref name="owner"/>'s guards in graph order. Empty when it has none, or when it is not in a
        /// graph that can tell this class it changed.
        /// </summary>
        public static List<ConditionalExecution> Of(BehaviorTreeNode owner)
        {
            if (owner == null) return None;

            var index = Instance.Of(owner.graph);

            if (index == null) return None;

            return index.TryGetValue(owner, out var guards) ? guards : None;
        }

        /// <summary>Drops the map and the subscriptions with it. See <see cref="GraphIndex"/>.</summary>
        public static void Invalidate() => Instance.Invalidate();
    }
}

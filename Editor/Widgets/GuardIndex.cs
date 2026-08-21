using System.Collections.Generic;
using UnityEditor;

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
    /// <b>The invalidation signal is the same one <see cref="Authoring.NodeProblemCache"/> uses</b>, and for
    /// the same reason: <c>graph.elements</c> already raises a change for every add and delete, so no editing
    /// path has to remember to call anything. Ownership cannot drift without one — every place that calls
    /// <c>ConditionalExecution.UpdateOwner</c> outside the tests adds the guard to <c>graph.elements</c> in
    /// the same breath, so there is no re-parent that this would miss. Undo is hooked separately because it
    /// re-instantiates elements rather than adding or removing them.
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
        private static readonly Dictionary<BehaviorTreeGraph, Dictionary<BehaviorTreeNode, List<ConditionalExecution>>>
            ByGraph = new();

        /// <summary>Handed back for a node with no guards, so the common case allocates nothing either.</summary>
        private static readonly List<ConditionalExecution> None = new();

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            // Undo restores elements by re-instantiating them rather than by adding to the collection, so the
            // collection event alone would leave the map keyed on objects nothing points at any more.
            Undo.undoRedoPerformed += Invalidate;
        }

        /// <summary>
        /// <paramref name="owner"/>'s guards in graph order. Empty when it has none, or when it is not in a
        /// graph that can tell this class it changed.
        /// </summary>
        public static List<ConditionalExecution> Of(BehaviorTreeNode owner)
        {
            if (owner == null) return None;

            var graph = owner.graph;

            if (graph == null) return None;

            if (!ByGraph.TryGetValue(graph, out var index))
            {
                index = Build(graph);
                ByGraph[graph] = index;

                graph.elements.CollectionChanged += Invalidate;
            }

            return index.TryGetValue(owner, out var guards) ? guards : None;
        }

        /// <summary>
        /// Drops every graph's map and the subscriptions with it, so nothing here holds a graph nobody has
        /// open. A change in one graph drops all of them: at the scale a canvas edits, one extra walk on the
        /// next repaint is cheaper than tracking which graph raised what.
        /// </summary>
        public static void Invalidate()
        {
            foreach (var graph in ByGraph.Keys)
            {
                if (graph != null) graph.elements.CollectionChanged -= Invalidate;
            }

            ByGraph.Clear();
        }

        private static Dictionary<BehaviorTreeNode, List<ConditionalExecution>> Build(BehaviorTreeGraph graph)
        {
            var index = new Dictionary<BehaviorTreeNode, List<ConditionalExecution>>();

            foreach (var graphElement in graph.elements)
            {
                if (graphElement is not ConditionalExecution guard) continue;

                var owner = guard.Owner;

                // A guard whose owner has gone is dangling; the canvas repair deletes it. Indexing it under
                // null would only invent a bucket nothing asks for.
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
    }
}

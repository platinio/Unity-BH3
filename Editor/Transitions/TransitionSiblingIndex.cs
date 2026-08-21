using System.Collections.Generic;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The transitions that run between the same two nodes, in either direction, grouped so a transition can
    /// find its siblings without asking the whole graph.
    ///
    /// <para>
    /// <b>Why.</b> Several transitions between one pair of nodes are drawn spread apart rather than on top of
    /// each other, and working out the spread means knowing the whole set and this one's place in it.
    /// <c>CachePosition</c> found that by walking every transition in the graph — once per transition, and
    /// with both main widgets invalidating their layout every frame, that is once per transition per repaint.
    /// Quadratic in the number of transitions, to answer a question about a set that is almost always one.
    /// </para>
    ///
    /// <para>
    /// <b>Direction is deliberately not part of the key.</b> The spread exists so that A→B and B→A do not
    /// draw on top of one another, so they have to land in the same group. <see cref="EndpointPair"/> is
    /// symmetric for exactly that reason, and a self-transition — where both ends are the same node — falls
    /// out of it without a special case.
    /// </para>
    ///
    /// <para>
    /// <b>Order within a group is graph order</b>, because the group is filled by walking
    /// <c>graph.Transitions</c> in order. That matters: the spread offset is computed by summing the sizes of
    /// the siblings <em>before</em> this one, so a group in a different order would move wires that nobody
    /// touched.
    /// </para>
    ///
    /// <para>
    /// Invalidated by <c>graph.elements.CollectionChanged</c> and by undo, the same signals
    /// <see cref="Authoring.NodeProblemCache"/> and <see cref="GuardIndex"/> use. Transitions are merged into
    /// <c>graph.elements</c>, so connecting or deleting one raises it; and a transition's endpoints are
    /// written by <c>SetupTransition</c> at construction, before it is added.
    /// </para>
    /// </summary>
    public static class TransitionSiblingIndex
    {
        /// <summary>
        /// Two nodes, unordered. Reference identity rather than <c>Equals</c>, because graph elements are
        /// identified by instance here and a node has no value equality to fall back on.
        /// </summary>
        private readonly struct EndpointPair : System.IEquatable<EndpointPair>
        {
            private readonly BehaviorTreeNode first;
            private readonly BehaviorTreeNode second;

            public EndpointPair(BehaviorTreeNode source, BehaviorTreeNode destination)
            {
                first = source;
                second = destination;
            }

            public bool Equals(EndpointPair other)
            {
                return (ReferenceEquals(first, other.first) && ReferenceEquals(second, other.second))
                    || (ReferenceEquals(first, other.second) && ReferenceEquals(second, other.first));
            }

            public override bool Equals(object obj) => obj is EndpointPair other && Equals(other);

            // XOR, so the hash is the same whichever way round the pair was built -- which is what makes it
            // agree with the symmetric Equals above.
            public override int GetHashCode()
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(first)
                     ^ System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(second);
            }
        }

        private static readonly Dictionary<BehaviorTreeGraph, Dictionary<EndpointPair, List<BehaviorTreeTransition>>>
            ByGraph = new();

        private static readonly List<BehaviorTreeTransition> None = new();

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            // Undo re-instantiates elements rather than adding to the collection, so the collection event
            // alone would leave groups keyed on nodes nothing points at any more.
            Undo.undoRedoPerformed += Invalidate;
        }

        /// <summary>
        /// Every transition drawn between <paramref name="source"/> and <paramref name="destination"/>,
        /// including the caller's own, in graph order.
        /// </summary>
        public static List<BehaviorTreeTransition> Of(
            BehaviorTreeGraph graph, BehaviorTreeNode source, BehaviorTreeNode destination)
        {
            if (graph == null || source == null || destination == null) return None;

            if (!ByGraph.TryGetValue(graph, out var index))
            {
                index = Build(graph);
                ByGraph[graph] = index;

                graph.elements.CollectionChanged += Invalidate;
            }

            return index.TryGetValue(new EndpointPair(source, destination), out var siblings) ? siblings : None;
        }

        /// <summary>
        /// Drops every graph's groups and the subscriptions with them, so nothing here holds a graph nobody
        /// has open.
        /// </summary>
        public static void Invalidate()
        {
            foreach (var graph in ByGraph.Keys)
            {
                if (graph != null) graph.elements.CollectionChanged -= Invalidate;
            }

            ByGraph.Clear();
        }

        private static Dictionary<EndpointPair, List<BehaviorTreeTransition>> Build(BehaviorTreeGraph graph)
        {
            var index = new Dictionary<EndpointPair, List<BehaviorTreeTransition>>();

            foreach (var transition in graph.Transitions)
            {
                if (transition?.source == null || transition.destination == null) continue;

                var key = new EndpointPair(transition.source, transition.destination);

                if (!index.TryGetValue(key, out var siblings))
                {
                    siblings = new List<BehaviorTreeTransition>();
                    index[key] = siblings;
                }

                siblings.Add(transition);
            }

            return index;
        }
    }
}

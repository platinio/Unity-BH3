using System.Collections.Generic;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The staleness rule for anything derived from a graph and read every repaint, written once.
    ///
    /// <para>
    /// <b>Why this exists.</b> The canvas asks the same shape of question in several places — a node's
    /// guards, a transition's siblings, a node's problems — where the answer is expensive to derive, cheap
    /// to store, and only wrong when the graph changes. Each answer was carrying its own copy of the
    /// machinery: a dictionary keyed by graph, a lazy build on the miss path, a
    /// <c>CollectionChanged</c> subscription taken out on first touch, an undo hook, and an
    /// <c>Invalidate</c> that unsubscribes everything and clears. That is one *decision* — what makes
    /// derived graph data stale, and how it is dropped — written three times, with two more in sight. A
    /// change to the rule was three edits that had to agree, and the next copy would inherit whichever
    /// version its author happened to read.
    /// </para>
    ///
    /// <para>
    /// <b>The rule, in one place.</b> An index is dropped when the graph's element collection changes, when
    /// undo runs, and when play mode starts or stops (<see cref="GraphCachePlayBoundary"/>). Element changes cover connect, disconnect, and every add or delete, because
    /// connections and transitions are merged into <c>graph.elements</c> alongside nodes. Undo is separate
    /// because it restores elements by re-instantiating them rather than by adding to the collection, so the
    /// collection event alone would leave a map keyed on objects nothing points at any more.
    /// </para>
    ///
    /// <para>
    /// <b>A change in one graph drops every graph's entry.</b> At the scale a canvas edits, one extra build
    /// on the next repaint is cheaper than tracking which graph raised what. Noted so the choice is on
    /// record rather than assumed; per-graph invalidation is a contained change if it is ever wanted.
    /// </para>
    ///
    /// <para>
    /// <b>Nothing may read an index from inside a <c>CollectionChanged</c> dispatch.</b> Editing helpers add
    /// an element and then finish wiring it up — <c>BehaviorTreeAuthoring.GuardOn*</c> adds the guard before
    /// calling <c>UpdateOwner</c>, because <c>AddNode</c> is what constructs it — so during the dispatch the
    /// graph is momentarily half-built. An index built from that state would bake in the half: the guard
    /// reads as ownerless and stays missing from its owner's stack until the next unrelated change. Every
    /// subscriber today only sets a flag or clears a dictionary, which is what keeps this safe; a subscriber
    /// that wanted to *read* derived data has to defer to the next frame instead.
    /// </para>
    /// </summary>
    public abstract class GraphIndex
    {
        /// <summary>
        /// Every index that has been constructed, so one undo hook serves all of them and a new index gets
        /// the behaviour by existing rather than by remembering to register.
        /// </summary>
        private static readonly List<GraphIndex> All = new();

        protected GraphIndex()
        {
            All.Add(this);
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            Undo.undoRedoPerformed += InvalidateAll;
        }

        /// <summary>
        /// Drops every index. Also the undo handler — indices are usually created lazily by a static field
        /// initializer, so registration can happen long after this hook is installed, which is why the list
        /// is walked at fire time rather than captured.
        /// </summary>
        public static void InvalidateAll()
        {
            foreach (var index in All) index.Invalidate();
        }

        /// <summary>Drops this index's entries and the graph subscriptions holding them fresh.</summary>
        public abstract void Invalidate();
    }

    /// <summary>
    /// A <typeparamref name="TValue"/> derived from each graph, built on first ask and dropped when that
    /// graph changes. Subclasses supply <see cref="Build"/> and a reading API of their own.
    /// </summary>
    public abstract class GraphIndex<TValue> : GraphIndex where TValue : class
    {
        private readonly Dictionary<BehaviorTreeGraph, TValue> byGraph = new();

        /// <summary>
        /// Derives the whole answer for one graph in a single pass. Called only on a miss, so it may be as
        /// expensive as the question needs.
        /// </summary>
        protected abstract TValue Build(BehaviorTreeGraph graph);

        /// <summary>
        /// This graph's entry, building it if there is not one. Null for a null graph, which is the caller's
        /// signal that there is nothing to answer from — a node not in a graph has nothing that could tell
        /// this class it changed.
        /// </summary>
        protected TValue For(BehaviorTreeGraph graph)
        {
            if (graph == null) return null;

            if (!byGraph.TryGetValue(graph, out var value))
            {
                value = Build(graph);
                byGraph[graph] = value;

                graph.elements.CollectionChanged += Invalidate;
            }

            return value;
        }

        public override void Invalidate()
        {
            foreach (var graph in byGraph.Keys)
            {
                if (graph != null) graph.elements.CollectionChanged -= Invalidate;
            }

            byGraph.Clear();
        }
    }
}

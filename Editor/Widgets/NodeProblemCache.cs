using System;
using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// What is wrong with each node, computed rarely and read every frame.
    ///
    /// <para>
    /// <b>When it runs is the whole design.</b> A canvas redraws constantly, and answering "what is wrong
    /// with this node" means comparing a contract against a referenced asset — allocating, and reading
    /// another object. Doing that per node per <c>OnGUI</c> is precisely the per-frame canvas work spec 10
    /// spends three sections removing, so it is cached and invalidated instead of polled.
    /// </para>
    ///
    /// <para>
    /// <b>The invalidation signal is deliberately borrowed rather than invented.</b>
    /// <see cref="FunctionEvaluator.Version"/> already exists to solve the identical problem one layer down —
    /// a cached plan that must not outlive an edit — and comparing one integer is what keeps it out of the
    /// call path. Reusing it buys a property worth more than the freshness itself: <b>the badge and the
    /// runtime cannot disagree</b>, because they go stale on the same signal. A node drawn as fine while
    /// evaluation would throw is not merely unlikely here, it is unrepresentable.
    /// </para>
    ///
    /// <para>
    /// <b>The second signal is the graph saying it changed.</b> The base problem every node reports is
    /// connection-dependent — <c>CollectProblems</c> flags <see cref="ValueInput.IsUnfedRequired"/>, which is
    /// false the moment something feeds the port — so the badge has to go stale on connect and disconnect or
    /// it survives the edit that fixes it. Rather than asking each editing call site to remember, this
    /// listens to <c>graph.elements</c>, which already raises a change for connections (they are merged into
    /// it alongside nodes and transitions) as well as for anything added or deleted. One rule in one place:
    /// a connect path added later cannot forget to call something it never had to call.
    /// </para>
    ///
    /// <para>
    /// <see cref="Invalidate"/> covers the rest — sub-tree contracts, undo, and the refresh verbs — and is
    /// bumped by whole events, never by a timer.
    /// </para>
    ///
    /// <para>
    /// <b>The one staleness this accepts.</b> A Function edited in the graph window and not yet saved bumps
    /// nothing, so the badge lags until it is. That is the right trade: the evaluator lags identically, so
    /// the badge is still telling the truth about what would happen on Play right now. Closing it would mean
    /// re-hashing every referenced graph per frame — the O(units) walk the binding plan exists to delete.
    /// </para>
    /// </summary>
    public static class NodeProblemCache
    {
        private static readonly Dictionary<BehaviorTreeNode, IReadOnlyList<NodeProblem>> Cache = new();

        /// <summary>
        /// The joined, human-readable form of each cached list, built on first ask and dropped with the
        /// entries it describes.
        ///
        /// <para>
        /// It exists because the badge's tooltip is built on every repaint of every problem-carrying node,
        /// for text that is only ever read on hover — a <c>StringBuilder</c> and a string per node per frame.
        /// The text is a pure function of the list, so it goes stale on exactly the same signal, and this
        /// class is where "computed rarely, read every frame" already lives.
        /// </para>
        /// </summary>
        private static readonly Dictionary<BehaviorTreeNode, string> Descriptions = new();

        private static readonly List<Func<BehaviorTreeNode, IEnumerable<NodeProblem>>> Providers = new();

        private static readonly List<NodeProblem> Scratch = new();

        /// <summary>
        /// The graphs whose element collection is currently being listened to, so the subscription can be
        /// taken back off again. Also what keeps a graph from being subscribed to twice, since every node in
        /// it arrives here separately.
        /// </summary>
        private static readonly HashSet<BehaviorTreeGraph> Observed = new();

        private static int evaluatorVersion = -1;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            // Undo can restore a contract, a connection or a whole node, and none of that goes through an
            // import. Cheapest correct answer is to drop everything and let it be asked again.
            Undo.undoRedoPerformed += Invalidate;
        }

        /// <summary>
        /// Adds a rule that can see something about a node the node cannot see about itself.
        ///
        /// <para>
        /// Nodes report their own problems by overriding <c>BehaviorTreeNode.CollectProblems</c>. This is the
        /// other half:
        /// a lint that lives outside the node — the kind <c>BehaviorTreeVerification</c> owns — contributes
        /// without the node having to know the rule exists, or the rule having to become a property of the
        /// thing it inspects.
        /// </para>
        /// </summary>
        public static void AddProvider(Func<BehaviorTreeNode, IEnumerable<NodeProblem>> provider)
        {
            if (provider == null || Providers.Contains(provider)) return;

            Providers.Add(provider);
            Invalidate();
        }

        /// <summary>
        /// Removes a previously added rule. Providers are static and live for the whole editor session, so
        /// anything registering one temporarily — a test, a tool that is open for a while — has to be able to
        /// take it away again rather than leaking it into every graph drawn afterwards.
        /// </summary>
        public static void RemoveProvider(Func<BehaviorTreeNode, IEnumerable<NodeProblem>> provider)
        {
            if (provider == null || !Providers.Remove(provider)) return;

            Invalidate();
        }

        /// <summary>
        /// Drops everything, so the next draw asks again. Call it on a whole event, not a tick.
        ///
        /// <para>
        /// The graph subscriptions go with it. Nothing is cached to keep fresh any more, and dropping them
        /// here is also what stops a graph nobody has open being held alive by this class: it is re-observed
        /// only if something asks about one of its nodes again.
        /// </para>
        /// </summary>
        public static void Invalidate()
        {
            foreach (var graph in Observed)
            {
                if (graph != null) graph.elements.CollectionChanged -= Invalidate;
            }

            Observed.Clear();
            Cache.Clear();
            Descriptions.Clear();
        }

        /// <summary>
        /// Starts listening to the graph a node belongs to, once per graph. False when there is no graph to
        /// listen to, which is the caller's signal not to cache what it is about to compute.
        ///
        /// <para>
        /// Called from the miss path rather than on every read: a hit means this graph was already observed
        /// when the entry was computed, because the only thing that drops entries also drops subscriptions.
        /// </para>
        /// </summary>
        private static bool Observe(BehaviorTreeNode node)
        {
            var graph = node.graph;

            if (graph == null) return false;

            if (Observed.Add(graph)) graph.elements.CollectionChanged += Invalidate;

            return true;
        }

        /// <summary>
        /// This node's problems, worst first. Empty when it is fine. Costs one integer comparison and a
        /// dictionary lookup on all but the first call after a change.
        /// </summary>
        public static IReadOnlyList<NodeProblem> For(BehaviorTreeNode node)
        {
            if (node == null) return Array.Empty<NodeProblem>();

            if (evaluatorVersion != FunctionEvaluator.Version)
            {
                evaluatorVersion = FunctionEvaluator.Version;
                Invalidate();
            }

            if (Cache.TryGetValue(node, out var cached)) return cached;

            // A node with no graph has nothing that can tell this class it changed, so its answer is computed
            // and handed back but never stored. Caching it would be the one way to get an entry that no
            // signal can ever drop -- a permanently wrong badge on the node the moment it joins a graph,
            // which is the exact failure the subscription above exists to prevent.
            var canGoStale = Observe(node);

            Scratch.Clear();

            // Every node can answer this — it is a virtual on the base with a do-nothing default, not a
            // capability some nodes have. A node that throws while describing itself must not take the canvas
            // down with it: a graph mid-edit is exactly when this is asked and exactly when a half-resolved
            // reference is likeliest.
            try
            {
                node.CollectProblems(Scratch);
            }
            catch (Exception exception)
            {
                Scratch.Add(new NodeProblem(NodeProblemSeverity.Error,
                    $"Could not determine this node's state: {exception.Message}"));
            }

            foreach (var provider in Providers)
            {
                try
                {
                    var extra = provider(node);
                    if (extra == null) continue;

                    foreach (var problem in extra) Scratch.Add(problem);
                }
                catch (Exception)
                {
                    // A broken provider is a bug in that provider, not a reason to stop drawing the graph.
                }
            }

            IReadOnlyList<NodeProblem> problems = Scratch.Count == 0
                ? Array.Empty<NodeProblem>()
                : Sorted(Scratch);

            if (canGoStale) Cache[node] = problems;

            return problems;
        }

        /// <summary>
        /// This node's problems as one block of text, a problem per line, for a tooltip. Empty when the node
        /// is fine.
        ///
        /// <para>
        /// Cached under the same rule as <see cref="For"/>, including the refusal to store an answer for a
        /// node with no graph: an entry nothing can invalidate is worse than recomputing one.
        /// </para>
        /// </summary>
        public static string DescriptionOf(BehaviorTreeNode node)
        {
            // First, because it is what applies the version check and can drop everything below.
            var problems = For(node);

            if (problems.Count == 0) return string.Empty;

            if (Descriptions.TryGetValue(node, out var cached)) return cached;

            var description = new System.Text.StringBuilder();

            var repairable = false;

            foreach (var problem in problems)
            {
                if (description.Length > 0) description.AppendLine();
                description.Append(problem);

                repairable |= problem.Repair != null;
            }

            // The badge's half of Unity-BH3#24: it names the defect, and this line is what tells the reader
            // the repair is one selection away instead of leaving them to find a context menu by accident.
            if (repairable)
            {
                description.AppendLine().AppendLine()
                    .Append("Select the node — its inspector can apply the fix.");
            }

            var text = description.ToString();

            // Only alongside a cached list. Storing text for a node whose list was not stored would outlive
            // every signal that could correct it.
            if (Cache.ContainsKey(node)) Descriptions[node] = text;

            return text;
        }

        /// <summary>The worst severity present, so a caller can pick one icon without reading the list.</summary>
        public static bool TryGetWorst(BehaviorTreeNode node, out NodeProblemSeverity severity, out int count)
        {
            var problems = For(node);

            severity = NodeProblemSeverity.Warning;
            count = problems.Count;

            if (count == 0) return false;

            foreach (var problem in problems)
            {
                if (problem.Severity != NodeProblemSeverity.Error) continue;

                severity = NodeProblemSeverity.Error;
                break;
            }

            return true;
        }

        private static NodeProblem[] Sorted(List<NodeProblem> problems)
        {
            var sorted = problems.ToArray();

            // Errors first: with several problems on one node, the one that stops it running is the one the
            // reader needs on the first line.
            Array.Sort(sorted, (left, right) => right.Severity.CompareTo(left.Severity));

            return sorted;
        }
    }
}

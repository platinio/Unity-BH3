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
    /// The local counter beside it covers what the evaluator does not care about: sub-tree contracts, undo,
    /// and the refresh verbs. Both are bumped by whole events, never by a timer.
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

        private static readonly List<Func<BehaviorTreeNode, IEnumerable<NodeProblem>>> Providers = new();

        private static readonly List<NodeProblem> Scratch = new();

        private static int evaluatorVersion = -1;

        private static int localVersion;

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
        /// Nodes report their own problems through <see cref="IReportsProblems"/>. This is the other half:
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

        /// <summary>Drops everything, so the next draw asks again. Call it on a whole event, not a tick.</summary>
        public static void Invalidate()
        {
            Cache.Clear();
            localVersion++;
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
                Cache.Clear();
            }

            if (Cache.TryGetValue(node, out var cached)) return cached;

            Scratch.Clear();

            if (node is IReportsProblems source)
            {
                // A node that throws while describing itself must not take the canvas down with it: a graph
                // mid-edit is exactly when this is asked and exactly when a half-resolved reference is
                // likeliest.
                try
                {
                    source.CollectProblems(Scratch);
                }
                catch (Exception exception)
                {
                    Scratch.Add(new NodeProblem(NodeProblemSeverity.Error,
                        $"Could not determine this node's state: {exception.Message}"));
                }
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

            Cache[node] = problems;
            return problems;
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

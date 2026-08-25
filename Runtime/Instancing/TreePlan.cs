using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// One behavior tree asset, baked for execution: every node given a dense index, every container's
    /// children resolved into priority order, every guard attached to its owner. Built once per asset and
    /// shared by every agent running it — the plan never changes at runtime, which is the entire premise.
    ///
    /// <para>
    /// This is the tree-sized sibling of <c>FunctionBindingPlan</c>: resolve the structure once, up front,
    /// so that per-agent work is only allocation and evaluation. Note what baking does <b>not</b> do — it
    /// never calls <see cref="BehaviorTreeGraph.OnAwake"/>, never arms guards onto nodes, never touches a
    /// child list on a container. Those are all mutations of node instances, and the nodes here belong to
    /// the shared asset. Everything the old awake path wrote into nodes, the plan holds beside them instead.
    /// </para>
    /// </summary>
    public sealed class TreePlan
    {
        /// <summary>Every node in the graph, indexed densely. Shared, never mutated.</summary>
        public readonly BehaviorTreeNode[] Nodes;

        /// <summary>The entry node's index.</summary>
        public readonly int EntryIndex;

        private readonly int[][] children;                 // [nodeIndex] -> child node indices, priority order
        private readonly ConditionalExecution[][] guards;  // [nodeIndex] -> guards owned by that node
        private readonly Dictionary<BehaviorTreeNode, int> indexOf;

        public int ChildCount(int nodeIndex) => children[nodeIndex].Length;
        public int ChildIndex(int nodeIndex, int ordinal) => children[nodeIndex][ordinal];
        public IReadOnlyList<ConditionalExecution> GuardsOn(int nodeIndex) => guards[nodeIndex];
        public int IndexOf(BehaviorTreeNode node) => indexOf[node];

        private TreePlan(BehaviorTreeNode[] nodes, int entryIndex, int[][] children,
            ConditionalExecution[][] guards, Dictionary<BehaviorTreeNode, int> indexOf)
        {
            Nodes = nodes;
            EntryIndex = entryIndex;
            this.children = children;
            this.guards = guards;
            this.indexOf = indexOf;
        }

        private static readonly Dictionary<BehaviorTreeGraphAsset, TreePlan> cache = new();

        /// <summary>The plan for an asset, baked on first request and shared afterwards.</summary>
        public static TreePlan For(BehaviorTreeGraphAsset asset)
        {
            if (cache.TryGetValue(asset, out var cached)) return cached;

            var plan = Bake(asset.graph);
            cache[asset] = plan;
            return plan;
        }

        /// <summary>
        /// Reads the graph into a plan. Read-only by construction: everything derived — indices, child
        /// order, guard ownership — lands in the plan's own arrays, so the graph is exactly as serialized
        /// when this returns.
        /// </summary>
        public static TreePlan Bake(BehaviorTreeGraph graph)
        {
            var nodeList = new List<BehaviorTreeNode>();
            var indexOf = new Dictionary<BehaviorTreeNode, int>();

            foreach (var node in graph.Nodes)
            {
                indexOf[node] = nodeList.Count;
                nodeList.Add(node);
            }

            var nodes = nodeList.ToArray();
            var children = new int[nodes.Length][];
            var guards = new ConditionalExecution[nodes.Length][];

            // The same priority derivation the old awake path uses to build container child lists —
            // called for its answer, not for its side effects.
            var byParent = graph.ChildrenByParentInPriorityOrder();

            var guardLists = new Dictionary<int, List<ConditionalExecution>>();

            for (int i = 0; i < nodes.Length; i++)
            {
                if (byParent.TryGetValue(nodes[i], out var kids))
                {
                    var indices = new int[kids.Count];
                    for (int k = 0; k < kids.Count; k++) indices[k] = indexOf[kids[k]];
                    children[i] = indices;
                }
                else
                {
                    children[i] = System.Array.Empty<int>();
                }

                // Guard attachment, resolved here per asset instead of armed onto nodes per agent — the
                // structural fix spec 07 predicted for spec 06's double-arming.
                if (nodes[i] is ConditionalExecution guard && guard.Owner != null
                    && indexOf.TryGetValue(guard.Owner, out int ownerIndex))
                {
                    if (!guardLists.TryGetValue(ownerIndex, out var list))
                    {
                        list = new List<ConditionalExecution>();
                        guardLists[ownerIndex] = list;
                    }

                    list.Add(guard);
                }
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                guards[i] = guardLists.TryGetValue(i, out var list)
                    ? list.ToArray()
                    : System.Array.Empty<ConditionalExecution>();
            }

            if (graph.EntryNode == null)
            {
                throw new System.InvalidOperationException("Cannot bake a plan for a graph with no entry node.");
            }

            return new TreePlan(nodes, indexOf[graph.EntryNode], children, guards, indexOf);
        }
    }
}

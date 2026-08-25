using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// Everything one agent knows about one running tree: three arrays and an agent reference. The plan
    /// carries the structure; this carries the state. Spawning an agent is allocating this — no graph
    /// clone, no serialize round trip.
    ///
    /// <para>
    /// This object is also where the debugger of the shared-runtime world would read from: node
    /// <paramref name="index"/>'s status, running flag and memory are each one array lookup, instead of
    /// fields scattered across a cloned graph.
    /// </para>
    /// </summary>
    public sealed class TreeInstance
    {
        public readonly TreePlan Plan;

        /// <summary>The agent this instance runs on.</summary>
        public readonly GameObject Agent;

        /// <summary>Per-node running flags — the shared-world <c>IsRunning</c>.</summary>
        public readonly bool[] Running;

        /// <summary>Per-node last status — the shared-world <c>LastExecutionStatus</c>.</summary>
        public readonly ExecutionStatus[] LastStatus;

        // One slot per node, filled on first use. Class-per-node in one array, exactly as spec 07's
        // starting point prescribes; a byte block with offsets is the later optimization if profiling
        // ever demands it.
        private readonly object[] memory;

        public TreeInstance(TreePlan plan, GameObject agent)
        {
            Plan = plan;
            Agent = agent;
            Running = new bool[plan.Nodes.Length];
            LastStatus = new ExecutionStatus[plan.Nodes.Length];
            memory = new object[plan.Nodes.Length];
        }

        /// <summary>
        /// The memory block for one node, created on first use. One type per node — two different answers
        /// to "what is this node's state" is an authoring error, and the slot cannot hold both.
        /// </summary>
        public T MemoryAt<T>(int nodeIndex) where T : class, new()
        {
            if (memory[nodeIndex] == null)
            {
                var created = new T();
                memory[nodeIndex] = created;
                return created;
            }

            if (memory[nodeIndex] is T typed) return typed;

            throw new System.InvalidOperationException(
                $"Node {nodeIndex} ({Plan.Nodes[nodeIndex].NodeName}) already stores its state as "
                + $"{memory[nodeIndex].GetType().Name}; it cannot also store it as {typeof(T).Name}.");
        }
    }
}

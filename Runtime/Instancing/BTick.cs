using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// One node's view of one tick on one agent — the whole world an executor body is allowed to see.
    ///
    /// <para>
    /// The split it enforces is the design: <see cref="Config{T}"/> reads the shared structure (a port's
    /// serialized default — one value for every agent), <see cref="Memory{T}"/> reads this agent's state
    /// (private to this node on this agent). A body written against these two cannot accidentally store
    /// per-agent state on the shared tree, because it was never handed the means.
    /// </para>
    /// </summary>
    public readonly struct BTick
    {
        public readonly TreeInstance Instance;
        public readonly int Index;

        public BTick(TreeInstance instance, int index)
        {
            Instance = instance;
            Index = index;
        }

        /// <summary>The shared node — structure and serialized config only. Never write to it.</summary>
        public BehaviorTreeNode Node => Instance.Plan.Nodes[Index];

        /// <summary>The agent this tick runs on.</summary>
        public GameObject Agent => Instance.Agent;

        /// <summary>This node's per-agent state, created on first use.</summary>
        public T Memory<T>() where T : class, new() => Instance.MemoryAt<T>(Index);

        /// <summary>
        /// A port's value from the shared structure. Demo scope: unconnected ports reading their serialized
        /// default — which never touches a machine. A port fed by a value-node chain evaluates source nodes,
        /// and threading the agent through that evaluation is the production design's work, not this demo's.
        /// </summary>
        public T Config<T>(ValueInput port) => port.GetValue<T>();

        /// <inheritdoc cref="Config{T}"/>
        public object Config(ValueInput port) => port.GetValue();

        public int ChildCount => Instance.Plan.ChildCount(Index);

        /// <summary>
        /// Ticks a child by ordinal, entering it first if it is not running — <c>ContainerNode.TickChild</c>
        /// with the entry decision (guards included) owned by the runner.
        /// </summary>
        public ExecutionStatus TickChild(int ordinal) =>
            SharedTreeRunner.TickNode(Instance, Instance.Plan.ChildIndex(Index, ordinal));

        /// <summary>Exits a child by ordinal if it is running. Safe to call for one that is not.</summary>
        public void ExitChild(int ordinal) =>
            SharedTreeRunner.ExitNode(Instance, Instance.Plan.ChildIndex(Index, ordinal));
    }
}

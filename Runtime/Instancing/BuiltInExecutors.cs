using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// The demo's node coverage: enough of the family to run a real authored tree — a composite with
    /// child-index state, a leaf with timer state, a stateless leaf, a per-tick transform leaf, and the
    /// entry. Each executor is the old node's body with its fields renamed into a memory class; read one
    /// beside its node and the migration cost per node is visible: it is that diff, node by node.
    /// </summary>
    internal static class BuiltInExecutors
    {
        public static void RegisterAll()
        {
            NodeExecutors.Register<Entry>(new EntryExecutor());
            NodeExecutors.Register<Selector>(new SelectorExecutor());
            NodeExecutors.Register<Sequence>(new SequenceExecutor());
            NodeExecutors.Register<WaitTime>(new WaitTimeExecutor());
            NodeExecutors.Register<DebugLog>(new DebugLogExecutor());
            NodeExecutors.Register<Rotate>(new RotateExecutor());
            NodeExecutors.Register<BooleanConditionalExecution>(new NeverTickedExecutor());
        }

        /// <summary>
        /// Guards are evaluated at their owner's door by the runner, never ticked as nodes — same as the
        /// old world, where they bypass the lifecycle entirely. Registered so a plan containing them does
        /// not read as "unsupported type"; reaching Tick means the runner walked somewhere it never should.
        /// </summary>
        private sealed class NeverTickedExecutor : NodeExecutor
        {
            public override ExecutionStatus Tick(in BTick tick) =>
                throw new System.InvalidOperationException(
                    $"'{tick.Node.NodeName}' is a guard; it is evaluated at its owner's entry, not ticked.");
        }

        /// <summary>Reports the tree below verbatim; enters via the tick so a refused entry is retried.</summary>
        private sealed class EntryExecutor : NodeExecutor
        {
            public override ExecutionStatus Tick(in BTick tick) =>
                tick.ChildCount == 0 ? ExecutionStatus.Success : tick.TickChild(0);

            public override void Exit(in BTick tick)
            {
                if (tick.ChildCount > 0) tick.ExitChild(0);
            }
        }

        /// <summary>What was <c>Composite.currentExecutingChildIndex</c>, per agent.</summary>
        private sealed class CompositeMemory
        {
            public int Current;
        }

        /// <summary>
        /// <see cref="Selector"/>'s walk: step over failures left to right, stop at the first Success or
        /// Running. The runner's entered-check replaces the <c>callOnEnter</c> flag — "this child is next"
        /// is simply "this child is not running when its turn comes". Preemption
        /// (<c>TryChangeRunningChild</c>) is out of demo scope; it needs the reactive guard walk.
        /// </summary>
        private sealed class SelectorExecutor : NodeExecutor
        {
            public override void Enter(in BTick tick) => tick.Memory<CompositeMemory>().Current = 0;

            public override ExecutionStatus Tick(in BTick tick)
            {
                var memory = tick.Memory<CompositeMemory>();

                while (memory.Current < tick.ChildCount)
                {
                    var result = tick.TickChild(memory.Current);

                    if (result == ExecutionStatus.Success) return ExecutionStatus.Success;

                    if (result == ExecutionStatus.Failure)
                    {
                        memory.Current++;
                        continue;
                    }

                    return result;
                }

                return ExecutionStatus.Failure;
            }

            public override void Exit(in BTick tick)
            {
                for (int i = 0; i < tick.ChildCount; i++) tick.ExitChild(i);
                tick.Memory<CompositeMemory>().Current = 0;
            }
        }

        /// <summary>Mirror of <see cref="Sequence"/>: stop at the first Failure, succeed past the end.</summary>
        private sealed class SequenceExecutor : NodeExecutor
        {
            public override void Enter(in BTick tick) => tick.Memory<CompositeMemory>().Current = 0;

            public override ExecutionStatus Tick(in BTick tick)
            {
                var memory = tick.Memory<CompositeMemory>();

                if (tick.ChildCount == 0) return ExecutionStatus.Success;

                while (memory.Current < tick.ChildCount)
                {
                    var result = tick.TickChild(memory.Current);

                    if (result == ExecutionStatus.Success)
                    {
                        memory.Current++;
                        continue;
                    }

                    if (result == ExecutionStatus.Failure)
                    {
                        memory.Current = 0;
                        return ExecutionStatus.Failure;
                    }

                    return result;
                }

                memory.Current = 0;
                return ExecutionStatus.Success;
            }

            public override void Exit(in BTick tick)
            {
                for (int i = 0; i < tick.ChildCount; i++) tick.ExitChild(i);
            }
        }

        /// <summary>What was <c>WaitTime.timer</c>, per agent.</summary>
        private sealed class WaitMemory
        {
            public float Remaining;
        }

        /// <summary>
        /// <see cref="WaitTime"/>, split along the line the demo exists to draw: the duration is config
        /// (shared, from the port), the countdown is memory (this agent's).
        /// </summary>
        private sealed class WaitTimeExecutor : NodeExecutor<WaitTime>
        {
            public override void Enter(in BTick tick) =>
                tick.Memory<WaitMemory>().Remaining = tick.Config<float>(Node(tick).Time);

            public override ExecutionStatus Tick(in BTick tick)
            {
                var memory = tick.Memory<WaitMemory>();
                memory.Remaining -= UnityEngine.Time.deltaTime;
                return memory.Remaining > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
            }
        }

        /// <summary>Stateless: config in, side effect out, no memory at all.</summary>
        private sealed class DebugLogExecutor : NodeExecutor<DebugLog>
        {
            public override ExecutionStatus Tick(in BTick tick)
            {
                Debug.Log($"{tick.Config(Node(tick).LogText)} [{tick.Agent.name}]", tick.Agent);
                return ExecutionStatus.Success;
            }
        }

        /// <summary>
        /// <see cref="Rotate"/> reads the agent from the tick each frame where the old node cached a
        /// transform at entry — same result, and nothing left to invalidate when agents pool.
        /// </summary>
        private sealed class RotateExecutor : NodeExecutor<Rotate>
        {
            public override ExecutionStatus Tick(in BTick tick)
            {
                var node = Node(tick);

                tick.Agent.transform.Rotate(
                    tick.Config<Vector3>(node.Axis),
                    tick.Config<float>(node.Speed) * UnityEngine.Time.deltaTime);

                return ExecutionStatus.Running;
            }
        }
    }
}

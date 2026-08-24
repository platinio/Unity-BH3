using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Instancing
{
    /// <summary>
    /// Ticks agents over shared plans. Owns the bookkeeping the old world keeps on node instances — the
    /// running flag, the last status, the entry decision with its guard check — so an executor body owns
    /// only its logic, mirroring how <c>BaseGraphNode.OnNodeEnter</c> / <c>OnUpdateInternal</c> /
    /// <c>OnNodeExit</c> wrap the old hooks.
    ///
    /// <para>
    /// The entry rule is <c>ContainerNode.TickChild</c>'s, made universal: a node is offered the door on
    /// the tick that would run it, guards are asked at that door, and a refusal is a <c>Failure</c> the
    /// parent sees on the same tick. Demo scope: guards are evaluated at entry only — the reactive walk
    /// (mid-run aborts, preemption bids) is production work, and its per-agent cache is exactly the state
    /// spec 13 maps into instance memory.
    /// </para>
    /// </summary>
    public static class SharedTreeRunner
    {
        /// <summary>Ticks a node: enter if not running (guards permitting), tick, exit on completion.</summary>
        public static ExecutionStatus TickNode(TreeInstance instance, int index)
        {
            var tick = new BTick(instance, index);

            if (!instance.Running[index])
            {
                var guards = instance.Plan.GuardsOn(index);

                for (int i = 0; i < guards.Count; i++)
                {
                    // Direct evaluation is honest for the demo's guards, which read a port default and
                    // keep no state. A stateful guard (ReactiveGuard's cache, GuardTrigger's versions)
                    // needs its state in instance memory first — spec 13's table says which fields.
                    if (!guards[i].Evaluate())
                    {
                        instance.LastStatus[index] = ExecutionStatus.Failure;
                        return ExecutionStatus.Failure;
                    }
                }

                instance.Running[index] = true;
                NodeExecutors.For(instance.Plan.Nodes[index]).Enter(tick);
            }

            var status = NodeExecutors.For(instance.Plan.Nodes[index]).Tick(tick);
            instance.LastStatus[index] = status;

            if (status != ExecutionStatus.Running) ExitNode(instance, index);

            return status;
        }

        /// <summary>Exits a node if it is running. Idempotent, like <c>OnNodeExit</c>'s early return.</summary>
        public static void ExitNode(TreeInstance instance, int index)
        {
            if (!instance.Running[index]) return;

            instance.Running[index] = false;
            NodeExecutors.For(instance.Plan.Nodes[index]).Exit(new BTick(instance, index));
        }
    }
}

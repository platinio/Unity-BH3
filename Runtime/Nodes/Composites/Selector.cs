using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Selector")]
    public class Selector : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Selector";
        public override string NodeName => "Selector";

        public override string Description => "Executes child nodes in order from left to right.\nExecution ends when any child node returns SUCCESS.";

        public override void OnEnter(BTContext ctx) => ctx.Memory<CompositeMemory>().Current = 0;

        /// <summary>
        /// A higher-priority child that would enter right now takes over from the one running.
        /// <para>
        /// Only children strictly above the current one are considered, so index order alone decides the
        /// winner — the first eligible child found is the highest-priority one, with no comparison needed.
        /// </para>
        /// </summary>
        protected override bool TryChangeRunningChild(in BTContext ctx, out int newChildIndex)
        {
            int current = ctx.Memory<CompositeMemory>().Current;
            newChildIndex = current;

            // Nothing outranks child 0, so there is no scan to run when it is the one executing.
            if (current <= 0) return false;

            int eligible = FirstChildThatWouldEnterNow(ctx, 0, current);
            if (eligible < 0) return false;

            newChildIndex = eligible;
            return true;
        }

        /// <summary>
        /// Walks the children left to right <em>within a single tick</em>, stepping over each one that fails
        /// and stopping at the first that returns Success or Running — after first giving a higher-priority
        /// sibling the chance to take the slot.
        ///
        /// <para>
        /// Migrated (spec 07 step 3), and the <c>callOnEnter</c> flag is gone rather than moved. It existed
        /// to say <em>this child is next</em> as distinct from <em>this child is live</em>, because entry
        /// was this loop's job. Entry is now <see cref="BTContext.TickChild"/>'s job — it enters a child
        /// that is not running and ticks one that is — so the distinction the flag carried is answered by
        /// the running state itself, on whichever side that state lives.
        /// </para>
        /// </summary>
        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            var memory = ctx.Memory<CompositeMemory>();

            // Decide who should run, then run them -- the scan happens before the running child is ticked,
            // never after.
            if (TryChangeRunningChild(ctx, out int preemptorIndex) && preemptorIndex != memory.Current)
            {
                var victim = ctx.Child(memory.Current);
                var preemptor = ctx.Child(preemptorIndex);

                // FirstTakeOverGuard() is written inline rather than into a local on purpose: the recorder
                // facade is [Conditional]-gated, and that removes the call site *including its arguments*, so
                // outside the editor and dev builds this lookup does not run at all.
                Debugging.BehaviorTreeRecorder.NodeTakenOver(victim, preemptor, preemptor.FirstTakeOverGuard());

                // The same call the Failure path below makes, so teardown parity is automatic rather than
                // a second implementation that has to be kept in step.
                ctx.ExitChild(memory.Current);

                memory.Current = preemptorIndex;

                // and fall into the loop, so the preemptor is entered and ticked on this same frame
            }

            while (memory.Current < ctx.ChildCount)
            {
                var result = ctx.TickChild(memory.Current);

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

        /// <summary>
        /// Exits every child that is still running, then forgets the resume point.
        /// <para>
        /// Written out rather than delegated to <c>base.OnExit(ctx)</c>: <see cref="ContainerNode"/> is
        /// still on the legacy hook (deliberately — see the note there), and its child sweep reads the
        /// node's own child list, which is empty on a shared tree. Exiting through the context is what
        /// makes this correct on both runtimes.
        /// </para>
        /// </summary>
        public override void OnExit(BTContext ctx)
        {
            for (int i = 0; i < ctx.ChildCount; i++) ctx.ExitChild(i);

            ctx.Memory<CompositeMemory>().Current = 0;
        }
    }
}

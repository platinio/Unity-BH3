using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Selector")]
    public class Selector : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Selector";
        public override string NodeName => "Selector";

        private bool callOnEnter = false;

        public override string Description => "Executes child nodes in order from left to right.\nExecution ends when any child node returns SUCCESS.";

        public override void OnEnter()
        {
            base.OnEnter();
            currentExecutingChildIndex = 0;
            callOnEnter = true;
        }

        /// <summary>
        /// A higher-priority child that would enter right now takes over from the one running.
        /// <para>
        /// Only children strictly above the current one are considered, so index order alone decides the
        /// winner — the first eligible child found is the highest-priority one, with no comparison needed.
        /// </para>
        /// </summary>
        protected override bool TryChangeRunningChild(out int newChildIndex)
        {
            newChildIndex = currentExecutingChildIndex;

            // Nothing outranks child 0, so there is no scan to run when it is the one executing.
            if (currentExecutingChildIndex <= 0) return false;

            int eligible = FirstChildThatWouldEnterNow(0, currentExecutingChildIndex);
            if (eligible < 0) return false;

            newChildIndex = eligible;
            return true;
        }

        /// <summary>
        /// Walks the children left to right <em>within a single tick</em>, stepping over each one that fails
        /// and stopping at the first that returns Success or Running — after first giving a higher-priority
        /// sibling the chance to take the slot.
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            var children = GetChildren();

            // Decide who should run, then run them -- the scan happens before the running child is ticked,
            // never after.
            //
            // Skipped on the frame this selector is itself entered (callOnEnter still true), because there
            // is no victim yet: nothing is mid-run to take over from.
            if (!callOnEnter && TryChangeRunningChild(out int preemptorIndex))
            {
                var victim = children[currentExecutingChildIndex];
                var preemptor = children[preemptorIndex];

                // FirstTakeOverGuard() is written inline rather than into a local on purpose: the recorder
                // facade is [Conditional]-gated, and that removes the call site *including its arguments*, so
                // outside the editor and dev builds this lookup does not run at all.
                Debugging.BehaviorTreeRecorder.NodeTakenOver(victim, preemptor, preemptor.FirstTakeOverGuard());

                // The same call the Failure path below makes, so teardown parity is automatic rather than
                // a second implementation that has to be kept in step.
                victim.OnNodeExit();

                currentExecutingChildIndex = preemptorIndex;
                callOnEnter = true;

                // and fall into the loop, so the preemptor is entered and ticked on this same frame
            }

            while (currentExecutingChildIndex < children.Count)
            {
                var task = children[currentExecutingChildIndex];

                // Do NOT make this unconditional — it reads as redundant and is not. OnUpdate runs once per
                // frame for as long as this selector is running, but OnEnter runs only when the parent
                // enters it. So on every frame after the first, this first iteration is *resuming* a child
                // that is already running, and entering it again would re-run its OnEnter every frame:
                // WaitTime would reset its timer to full, an animation would restart, RandomChance would
                // re-roll. Any multi-frame action would hang forever.
                // The flag is only ever false here, on that resume; after a Failure below it is set back to
                // true so the next child does get entered. Pinned by ARunningChild_IsTickedAgainButNotReEntered.
                if (callOnEnter)
                {
                    callOnEnter = false;
                    task.OnNodeEnter();
                }

                var result = task.OnUpdateInternal();

                if (result == ExecutionStatus.Success)
                {
                    task.OnNodeExit();
                    return ExecutionStatus.Success;
                }

                if (result == ExecutionStatus.Failure)
                {
                    task.OnNodeExit();

                    currentExecutingChildIndex++;
                    callOnEnter = true;
                    continue;
                }
               
                return result;
            }

            return ExecutionStatus.Failure;
        }

        public override void OnExit()
        {
            base.OnExit();
            currentExecutingChildIndex = 0;
        }
    }
}

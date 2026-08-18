using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Sequence")]
    public class Sequence : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Sequence";
        public override string NodeName => "Sequence";

        private bool callOnEnter = false;
        public override string Description => "Executes child nodes in order from left to right.\nExecution ends when any child node returns FAILURE.";

        public override void OnEnter()
        {
            base.OnEnter();
            currentExecutingChildIndex = 0;
            callOnEnter = true;
        }
      
        public override ExecutionStatus OnUpdate()
        {
            var children = GetChildren();
            if (children.Count == 0) return ExecutionStatus.Success;

            while (currentExecutingChildIndex < children.Count)
            {
                var task = children[currentExecutingChildIndex];

                // Do NOT make this unconditional — it reads as redundant and is not. OnUpdate runs once per
                // frame for as long as this sequence is running, but OnEnter runs only when the parent
                // enters it. So on every frame after the first, this first iteration is *resuming* a child
                // that is already running, and entering it again would re-run its OnEnter every frame:
                // WaitTime would reset its timer to full, an animation would restart, RandomChance would
                // re-roll. Any multi-frame action would hang forever.
                // The flag is only ever false here, on that resume; after a Success below it is set back to
                // true so the next child does get entered. Pinned by ARunningChild_IsTickedAgainButNotReEntered.
                // Cleared *after* the entry, not before. If OnNodeEnter throws — an unfed required port, a
                // guard whose condition faults — clearing first would leave this sequence believing it had
                // entered a child it had not, and the next tick would go straight to OnUpdateInternal on a
                // node whose OnEnter never ran. That is the silent-success hazard again, arriving through a
                // cached flag rather than through IsRunning: the entry is retried, visibly, every frame.
                if (callOnEnter)
                {
                    task.OnNodeEnter();
                    callOnEnter = false;
                }

                var result = task.OnUpdateInternal();

                if (result == ExecutionStatus.Success)
                {
                    task.OnNodeExit();

                    currentExecutingChildIndex++;
                    callOnEnter = true;
                    continue;
                }

                if (result == ExecutionStatus.Failure)
                {
                    task.OnNodeExit();

                    currentExecutingChildIndex = 0;
                    callOnEnter = true;
                    return ExecutionStatus.Failure;
                }
             
                return result;
            }

            currentExecutingChildIndex = 0;
            callOnEnter = true;
            return ExecutionStatus.Success;
        }
    }
}

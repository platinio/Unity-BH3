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
        /// Walks the children left to right <em>within a single tick</em>, stepping over each one that fails
        /// and stopping at the first that returns Success or Running.
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            var children = GetChildren();
      
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

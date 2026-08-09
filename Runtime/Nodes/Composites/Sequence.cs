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

        /// <summary>
        /// Walks the children left to right <em>within a single tick</em>, stepping over each one that
        /// succeeds and stopping at the first that returns Failure or Running. The mirror image of
        /// <see cref="Selector"/>; see that node for why the descent must complete on one frame.
        /// <para>
        /// The loop terminates because every iteration either returns or advances
        /// <see cref="Composite.currentExecutingChildIndex"/>, which is bounded by the child count.
        /// </para>
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            var children = GetChildren();
            if (children.Count == 0) return ExecutionStatus.Success;

            while (currentExecutingChildIndex < children.Count)
            {
                var task = children[currentExecutingChildIndex];

                if (callOnEnter)
                {
                    callOnEnter = false;
                    task.OnNodeEnter();
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

                // Running — the child owns the rest of this frame.
                return result;
            }

            currentExecutingChildIndex = 0;
            callOnEnter = true;
            return ExecutionStatus.Success;
        }
    }
}

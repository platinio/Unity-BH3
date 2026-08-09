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
        /// <para>
        /// A tick asks "given the world right now, what should this agent be doing?", so the answer has to be
        /// reached on the frame the question is asked. Yielding a frame per failed child would make a ten-branch
        /// selector take ten frames to reach its last branch, and the agent would then act on a world state that
        /// is nine frames stale. Only a child that is genuinely still working — Running — ends the tick.
        /// </para>
        /// <para>
        /// The loop terminates because every iteration either returns or advances
        /// <see cref="Composite.currentExecutingChildIndex"/>, which is bounded by the child count.
        /// </para>
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            var children = GetChildren();

            // A Selector is an OR over its children, so an empty one is an empty OR: Failure. Reporting
            // Success would tell the parent this branch handled the situation when it did nothing at all,
            // hiding a mis-authored tree behind a green result. Matches ParallelSelector, and mirrors
            // Sequence, which is an AND and so succeeds when empty.
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
                    return ExecutionStatus.Success;
                }

                if (result == ExecutionStatus.Failure)
                {
                    task.OnNodeExit();

                    currentExecutingChildIndex++;
                    callOnEnter = true;
                    continue;
                }

                // Running — the child owns the rest of this frame.
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

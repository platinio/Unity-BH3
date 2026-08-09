using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    public class Parallel : Composite
    {
        protected ExecutionStatus[] childrenTaskStatus;

        public override void OnEnter()
        {
            base.OnEnter();

            // Sized against the current child count rather than allocated once. The array is indexed by the
            // same loop that walks GetChildren(), so a count that grew after the first enter would index past
            // the end; re-checking the length is cheaper than the exception it avoids.
            int childCount = GetChildren().Count;
            if (childrenTaskStatus == null || childrenTaskStatus.Length != childCount)
            {
                childrenTaskStatus = new ExecutionStatus[childCount];
            }

            ResetChildrenTaskStatus();

            for (int n = 0; n < children.Count; n++)
            {
                children[n].OnNodeEnter();
            }
        }

        // No OnExit override: ContainerNode.OnExit already exits every child, and exiting them a second time
        // here did nothing because OnNodeExit early-returns on a node that is no longer running.

        private void ResetChildrenTaskStatus()
        {
            for (int n = 0; n < childrenTaskStatus.Length; n++)
            {
                childrenTaskStatus[n] = ExecutionStatus.Running;
            }
        }
    }
}

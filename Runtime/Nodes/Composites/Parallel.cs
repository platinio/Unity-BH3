using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    public class Parallel : Composite
    {
        protected ExecutionStatus[] childrenTaskStatus;

        public override void OnEnter()
        {
            base.OnEnter();
      
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

        private void ResetChildrenTaskStatus()
        {
            for (int n = 0; n < childrenTaskStatus.Length; n++)
            {
                childrenTaskStatus[n] = ExecutionStatus.Running;
            }
        }
    }
}

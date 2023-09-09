
using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    public class Parallel : Composite
    {
        protected ExecutionStatus[] childrenTaskStatus;

        public override void OnEnter()
        {
            if (childrenTaskStatus == null)
            {
                childrenTaskStatus = new ExecutionStatus[GetChildren().Count];
            }
            ResetChildrenTaskStatus();
        }

        private void ResetChildrenTaskStatus()
        {
            for (int n = 0; n < childrenTaskStatus.Length; n++)
            {
                childrenTaskStatus[n] = ExecutionStatus.Inactive;
            }
        }
    }
}
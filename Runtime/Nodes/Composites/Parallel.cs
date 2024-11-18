using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    public class Parallel : Composite
    {
        protected ExecutionStatus[] childrenTaskStatus;

        public override void OnEnter()
        {
            base.OnEnter();
            if (childrenTaskStatus == null)
            {
                childrenTaskStatus = new ExecutionStatus[GetChildren().Count];
            }
            ResetChildrenTaskStatus();
            
            for (int n = 0; n < children.Count; n++)
            {
                children[n].OnNodeEnter();
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            for (int n = 0; n < children.Count; n++)
            {
                children[n].OnNodeExit();
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
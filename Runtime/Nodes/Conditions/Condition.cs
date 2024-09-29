using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    public class Condition : BehaviorTreeNode
    {
        public override bool ShowIcon => true;

        public virtual bool Evaluate() => false;

        public override ExecutionStatus OnUpdate()
        {
            return Evaluate() ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }
    }
}


using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    public class Condition : BehaviorTreeNode
    {
        public virtual bool Evaluate() => false;

        public override ExecutionStatus OnUpdate()
        {
            return Evaluate() ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }
    }
}


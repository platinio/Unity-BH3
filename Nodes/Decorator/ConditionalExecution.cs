using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public abstract class ConditionalExecution : GameplayNode
    {
        [Serialize] private BehaviorTreeNode owner;

        public override string NodeName => "Test Conditional Execution";

        public BehaviorTreeNode Owner => owner;
        
        public ConditionalExecution(BehaviorTreeNode owner)
        {
            this.owner = owner;
        }

        public bool EvaluateInternal()
        {
            bool result = Evaluate();
            LastExecutionStatus = result ? ExecutionStatus.Success : ExecutionStatus.Failure;

            return result;
        }
        
        public abstract bool Evaluate();
    }
}


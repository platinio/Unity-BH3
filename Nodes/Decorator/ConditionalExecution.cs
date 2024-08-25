using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public class ConditionalExecution : GameplayNode
    {
        [Serialize] private BehaviorTreeNode owner;

        public override string NodeName => "Conditional Execution";

        public BehaviorTreeNode Owner => owner;
        
        public ConditionalExecution(BehaviorTreeNode owner)
        {
            this.owner = owner;
        }
    }
}


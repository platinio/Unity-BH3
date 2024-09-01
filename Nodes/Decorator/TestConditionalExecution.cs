using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    
    public class TestConditionalExecution : ConditionalExecution
    {
        [Serialize, Inspectable] private bool conditionalExecutionValue;
        
        public TestConditionalExecution(BehaviorTreeNode owner) : base(owner)
        {
        }

        public override bool Evaluate() => conditionalExecutionValue;
    }
}
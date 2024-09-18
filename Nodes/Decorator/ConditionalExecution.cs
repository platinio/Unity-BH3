using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [SpecialNode]
    public abstract class ConditionalExecution : GameplayNode
    {
        [Serialize] private BehaviorTreeNode owner;
       
        public BehaviorTreeNode Owner => owner;

        public void UpdateOwner(BehaviorTreeNode owner)
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

        public override void BeforeRemove()
        {
            base.BeforeRemove();

            foreach (var node in graph.Nodes)
            {
                node.OnRemoveConditionalExecution(this);
            }
        }
    }
}


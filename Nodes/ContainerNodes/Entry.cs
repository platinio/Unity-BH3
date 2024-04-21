using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// entry point for a behavior tree graph
    /// </summary>
    public class Entry : ContainerNode
    {
        public override string NodeName => "Entry";
        public override bool CanDelete => false;
        public override int MaxChildrenLimit => 1;
        
        protected override string NodeIconPath => "NodeIcons/Entry";

        private bool isComplete = false;
        
        public override void OnEnter()
        {
            isComplete = false;
            foreach (var children in GetChildren())
            {
                children.OnNodeEnter();
            }
        }

        public override void OnExit()
        {
            foreach (var children in GetChildren())
            {
                children.OnNodeExit();
            }
        }

        public override ExecutionStatus OnUpdate()
        {
            if (isComplete) return ExecutionStatus.Success;
            
            if (CanExecute)
            {
                if (GetChildren().Count == 0)
                {
                    isComplete = true;
                    return ExecutionStatus.Success;
                }
                
                var result =  GetChildren()[0].OnUpdateInternal();
                isComplete = result == ExecutionStatus.Success || result == ExecutionStatus.Failure;
            }

            return ExecutionStatus.Running;
        }
    }
}
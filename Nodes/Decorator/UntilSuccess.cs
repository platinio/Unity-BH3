using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Until Success")]
    public class UntilSuccess : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/UntilSuccess";
        public override string NodeName => "Until Success";
        
        public override int MaxChildrenLimit => 1;
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[0];
            var result = task.OnUpdateInternal();

            if (result != ExecutionStatus.Success)
            {
                return ExecutionStatus.Running;
            }

            task.OnNodeExit();
            return ExecutionStatus.Success;
        }
    }
}
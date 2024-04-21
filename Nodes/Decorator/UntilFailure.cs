using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Until Failure")]
    public class UntilFailure : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/UntilFailure";
        public override string NodeName => "Until Failure";
        
        public override int MaxChildrenLimit => 1;
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[0];
            var result = task.OnUpdateInternal();

            if (result != ExecutionStatus.Failure)
            {
                return ExecutionStatus.Running;
            }

            task.OnNodeExit();
            return ExecutionStatus.Success;
        }
    }
}
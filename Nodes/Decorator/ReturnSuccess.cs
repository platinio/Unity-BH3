using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Decorator/Create Return Success")]
    public class ReturnSuccess : Decorator
    {
        public override string NodeName => "Return Success";
        protected override string NodeIconPath => "NodeIcons/ReturnSuccess";
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[0];
            var result = task.OnUpdate();

            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                return ExecutionStatus.Success;
            }

            return ExecutionStatus.Running;
        }
    }
}
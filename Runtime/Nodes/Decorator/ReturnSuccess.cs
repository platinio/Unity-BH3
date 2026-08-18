using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Return Success")]
    public class ReturnSuccess : Decorator
    {
        public override string NodeName => "Return Success";
        protected override string NodeIconPath => "NodeIcons/ReturnSuccess";
        public override string Description => "Overrides child return value with SUCCESS";
        
        public override int MaxChildrenLimit => 1;
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[0];
            var result = TickChild(task);

            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                return ExecutionStatus.Success;
            }

            return ExecutionStatus.Running;
        }
    }
}
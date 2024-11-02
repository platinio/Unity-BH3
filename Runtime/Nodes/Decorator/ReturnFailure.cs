using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Return Failure")]
    public class ReturnFailure : Decorator
    {
        public override string NodeName => "Return Failure";
        protected override string NodeIconPath => "NodeIcons/ReturnFailure";
        public override string Description => "Overrides child return value with FAILURE";
        public override int MaxChildrenLimit => 1;

        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Failure; 
            
            var task = GetChildren()[0];
            var result = task.OnUpdateInternal();

            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                return ExecutionStatus.Failure;
            }

            return ExecutionStatus.Running;
        }
    }
}
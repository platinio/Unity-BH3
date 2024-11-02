using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Until Failure")]
    public class UntilFailure : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/UntilFailure";
        public override string NodeName => "Until Failure";
        public override string Description => "Runs child until return FAILURE";
        public override int MaxChildrenLimit => 1;
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[0];
            var result = task.OnUpdateInternal();

            if (result != ExecutionStatus.Failure)
            {
                if (result == ExecutionStatus.Success)
                {
                    //lets enter and exit the node to prepare to run again
                    task.OnNodeExit();
                    task.OnNodeEnter();
                }
            
                return ExecutionStatus.Running;
            }

            task.OnNodeExit();
            return ExecutionStatus.Success;
        }
    }
}
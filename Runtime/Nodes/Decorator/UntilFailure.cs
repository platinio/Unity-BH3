using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Until Failure")]
    public class UntilFailure : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/UntilFailure";
        public override string NodeName => "Until Failure";
        public override string Description => "Executes child node until it returns FAILURE";
        public override int MaxChildrenLimit => 1;
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[0];
            var result = TickChild(task);

            if (result != ExecutionStatus.Failure)
            {
                // Exit only. The next tick enters it -- see ContainerNode.TickChild -- so a restart cannot
                // be refused on one frame and then ticked as though it had taken on the next.
                if (result == ExecutionStatus.Success)
                {
                    task.OnNodeExit();
                }

                return ExecutionStatus.Running;
            }

            task.OnNodeExit();
            return ExecutionStatus.Success;
        }
    }
}
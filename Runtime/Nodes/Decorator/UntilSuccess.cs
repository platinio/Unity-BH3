using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Until Success")]
    public class UntilSuccess : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/UntilSuccess";
        public override string Description => "Executes child node until it returns SUCCESS";
        public override string NodeName => "Until Success";
        
        public override int MaxChildrenLimit => 1;
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[0];
            var result = TickChild(task);

            if (result != ExecutionStatus.Success)
            {
                // Exit only. The next tick enters it -- see ContainerNode.TickChild -- so a restart cannot
                // be refused on one frame and then ticked as though it had taken on the next.
                if (result == ExecutionStatus.Failure)
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
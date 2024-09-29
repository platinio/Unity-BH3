using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
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
                if (result == ExecutionStatus.Failure)
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
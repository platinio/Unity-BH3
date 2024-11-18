using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Selector")]
    public class Selector : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Selector";
        public override string NodeName => "Selector";

        private bool callOnEnter = false;

        public override string Description => "Executes child nodes in order from left to right.\nExecution ends when any child node returns FAILURE.";

        public override void OnEnter()
        {
            base.OnEnter();
            currentExecutingChildIndex = 0;
            callOnEnter = true;
        }
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success;

            var task = GetChildren()[currentExecutingChildIndex];
            
            if (callOnEnter)
            {
                callOnEnter = false;
                task.OnNodeEnter();
            }
            
            var result = task.OnUpdateInternal();

            if (result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                return ExecutionStatus.Success;
            }
            if (result == ExecutionStatus.Failure)
            {
                task.OnNodeExit();
               
                currentExecutingChildIndex++;
                if (GetChildren().Count <= currentExecutingChildIndex) return ExecutionStatus.Failure;
                
                GetChildren()[currentExecutingChildIndex].OnNodeEnter();
                return ExecutionStatus.Running;
            }

            return result;
        }

        public override void OnExit()
        {
            base.OnExit();
            currentExecutingChildIndex = 0;
        }
    }
}
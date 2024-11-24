using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Sequence")]
    public class Sequence : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Sequence";
        public override string NodeName => "Sequence";

        private bool callOnEnter = false;
        public override string Description => "Executes child nodes in order from left to right.\nExecution ends when any child node returns SUCCESS.";

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
                currentExecutingChildIndex++;
                if (GetChildren().Count <= currentExecutingChildIndex) return ExecutionStatus.Success;
                
                GetChildren()[currentExecutingChildIndex].OnNodeEnter();
                return ExecutionStatus.Running;
            }
            if (result == ExecutionStatus.Failure)
            {
                task.OnNodeExit();
                currentExecutingChildIndex = 0;
                return ExecutionStatus.Failure;
            }

            return result;
        }
    }
}
using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Sequence")]
    public class Sequence : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Sequence";
        public override string NodeName => "Sequence";

        private bool callOnEnter = false;
        
        public override void OnEnter()
        {
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
                task.OnEnter();
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

        public override void OnExit()
        {
            currentExecutingChildIndex = 0;
        }
    }
}
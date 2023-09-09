using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Sequence")]
    public class Sequence : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Sequence";
        public override string NodeName => "Sequence";

        public override void OnEnter()
        {
            currentExecutingChildIndex = 0;
            
            if (GetChildren().Count == 0) return;
            GetChildren()[0].OnNodeEnter();
        }

        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success;

            var task = GetChildren()[currentExecutingChildIndex];
            var result = task.OnUpdate();

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
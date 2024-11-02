using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Decorator/Create Repeater")]
    public class Repeater : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/Cycle";
        public override string NodeName => "Repeater";
        public override bool CanExecute => true;
        public override int MaxChildrenLimit => 1;
        private int currentExecutingChildIndex = 0;

        public override string Description => "Force child execution and doesnt return";

        public override void OnEnter()
        {
            base.OnEnter();
            currentExecutingChildIndex = 0;
        }

        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count <= 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[currentExecutingChildIndex];
            var result = task.OnUpdateInternal();

            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                currentExecutingChildIndex++;
                
                if (GetChildren().Count <= currentExecutingChildIndex)
                {
                    currentExecutingChildIndex = 0;
                }

                GetChildren()[currentExecutingChildIndex].OnNodeEnter();
            }

            return ExecutionStatus.Running;

        }
    }
}
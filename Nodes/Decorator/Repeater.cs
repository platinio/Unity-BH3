using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Decorator/Create Repeater")]
    public class Repeater : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/Cycle";
        public override string NodeName => "Repeater";
        public override bool CanExecute => true;

        public override ExecutionStatus OnUpdate()
        {
            var task = GetChildren()[0];
            var result = task.OnUpdate();

            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                task.OnNodeEnter();
            }

            return ExecutionStatus.Running;
        }
    }
}
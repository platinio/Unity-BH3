using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Decorator/Repeater")]
    public class Repeater : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/Cycle";
        public override string NodeName => "Repeater";
        public override bool CanExecute => true;
        public override int MaxChildrenLimit => 1;

        public override string Description => "Force child execution and doesnt return";

        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count <= 0) return ExecutionStatus.Success;

            var task = GetChildren()[0];
            var result = TickChild(task);

            // Exited but not re-entered: the next tick enters it, because that is where entry lives now.
            // Restarting it here would decide "the child may start" on this frame and act on that decision
            // on the next one -- and if the guard said no in between, the restart was refused and the tick
            // that followed ran a child that never entered. Deferring costs nothing: the child's next
            // OnUpdate lands on the next frame either way.
            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
            }

            return ExecutionStatus.Running;
        }
    }
}

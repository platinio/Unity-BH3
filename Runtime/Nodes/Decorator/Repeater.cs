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

        public override string Description => "Force child execution and doesnt return";

        /// <summary>
        /// Restarts the child every time it completes and never reports completion itself.
        /// <para>
        /// One iteration per tick is deliberate, not the single-tick-descent bug the composites had: a
        /// repeater whose child finishes instantly would spin forever if it restarted within the same frame.
        /// The frame boundary is what bounds the loop, so it stays.
        /// </para>
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count <= 0) return ExecutionStatus.Success;

            var task = GetChildren()[0];
            var result = task.OnUpdateInternal();

            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                task.OnNodeEnter();
            }

            return ExecutionStatus.Running;
        }
    }
}

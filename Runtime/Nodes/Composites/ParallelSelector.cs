using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// selector task running all children at the same time
    /// </summary>
    [GraphCreateMenu("Composite/Parallel Selector")]
    public class ParallelSelector : Parallel
    {
        public override string NodeName => "Parallel Selector";
        protected override string NodeIconPath => "NodeIcons/ParallelSelector";

        public override string Description => "Executes child nodes at the same time.\nExecution ends when any child node returns SUCCESS.";

        public override ExecutionStatus OnUpdate()
        {
            for (int n = 0; n < GetChildren().Count; n++)
            {
                if (childrenTaskStatus[n] == ExecutionStatus.Failure) continue;
                if (childrenTaskStatus[n] == ExecutionStatus.Success) return ExecutionStatus.Success;
                
                childrenTaskStatus[n] = GetChildren()[n].OnUpdateInternal();
            }

            return GetTaskStatus();
        }

        private ExecutionStatus GetTaskStatus()
        {
            foreach (var taskStatus in childrenTaskStatus)
            {
                if (taskStatus == ExecutionStatus.Success) return ExecutionStatus.Success;
                if (taskStatus != ExecutionStatus.Failure) return ExecutionStatus.Running;
            }

            return ExecutionStatus.Failure;
        }
    }
}
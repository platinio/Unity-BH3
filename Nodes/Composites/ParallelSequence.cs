using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// The Parallel task acts in a similar way to the Sequence task. It has a set of child tasks,
    /// and it runs them until one of them fails. At that point, the Parallel task as a whole fails.
    /// If all of the child tasks complete successfully, the Parallel task returns with success
    /// </summary>
    [GraphCreateMenu("Composite/Create Parallel Sequence")]
    public class ParallelSequence : Parallel
    {
        protected override string NodeIconPath => "NodeIcons/ParallelSequence";
        public override string NodeName => "Parallel Sequence";

        public override ExecutionStatus OnUpdate()
        {
            for (int n = 0; n < GetChildren().Count; n++)
            {
                if (childrenTaskStatus[n] != ExecutionStatus.Success) continue;
                childrenTaskStatus[n] = GetChildren()[n].OnUpdateInternal();
            }

            return GetTaskStatus();
        }

        private ExecutionStatus GetTaskStatus()
        {
            foreach (var taskStatus in childrenTaskStatus)
            {
                if (taskStatus == ExecutionStatus.Failure) return ExecutionStatus.Failure;
                if (taskStatus != ExecutionStatus.Success) return ExecutionStatus.Running;
            }

            return ExecutionStatus.Success;
        }
    }
}
using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    /// <summary>
    /// selector task running all children at the same time
    /// </summary>
    [GraphCreateMenu("Composite/Create Parallel Selector")]
    public class ParallelSelector : Parallel
    {
        public override string NodeName => "Parallel Selector";
        protected override string NodeIconPath => "NodeIcons/ParallelSelector";

        public override ExecutionStatus OnUpdate()
        {
            for (int n = 0; n < GetChildren().Count; n++)
            {
                if (m_childrenTaskStatus[n] != ExecutionStatus.Failure) continue;
                m_childrenTaskStatus[n] = GetChildren()[n].OnUpdate();
            }

            return GetTaskStatus();
        }

        private ExecutionStatus GetTaskStatus()
        {
            foreach (var taskStatus in m_childrenTaskStatus)
            {
                if (taskStatus == ExecutionStatus.Success) return ExecutionStatus.Success;
                if (taskStatus != ExecutionStatus.Failure) return ExecutionStatus.Running;
            }

            return ExecutionStatus.Failure;
        }
    }
}

using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    public class Parallel : Composite
    {
        protected ExecutionStatus[] m_childrenTaskStatus;

        public override void OnEnter()
        {
            if (m_childrenTaskStatus == null)
            {
                m_childrenTaskStatus = new ExecutionStatus[GetChildren().Count];
            }
            ResetChildrenTaskStatus();
        }

        private void ResetChildrenTaskStatus()
        {
            for (int n = 0; n < m_childrenTaskStatus.Length; n++)
            {
                m_childrenTaskStatus[n] = ExecutionStatus.Inactive;
            }
        }
    }
}
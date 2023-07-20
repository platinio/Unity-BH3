using Platinio.GraphCore;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Stop NavAgent")]
    public class StopNavAgent : GameplayNode
    {
        public override string NodeName => "Stop NavAgent";

        private NavMeshAgent m_navAgent = null;

        public override void OnAwake()
        {
            m_navAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
            m_navAgent.isStopped = true;
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }
    }
}
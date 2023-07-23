using Platinio.GraphCore;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Wait Until Reach Nav Target Position")]
    public class WaitUntilReachNavTargetPosition : GameplayNode
    {
        public override string NodeName => "Wait Nav Agent Position";

        private NavMeshAgent m_navAgent = null;

        public override void OnAwake()
        {
            m_navAgent = GetComponent<NavMeshAgent>();
        }

        public override ExecutionStatus OnUpdate() => m_navAgent.remainingDistance < Mathf.Epsilon ? ExecutionStatus.Success : ExecutionStatus.Running;
    }
}
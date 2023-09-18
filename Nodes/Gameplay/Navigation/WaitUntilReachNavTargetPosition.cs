using Platinio.GraphCore;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Wait Until Reach Nav Target Position")]
    public class WaitUntilReachNavTargetPosition : GameplayNode
    {
        public override string NodeName => "Wait Until Reach Nav Target Position";

        private NavMeshAgent navAgent = null;

        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        public override ExecutionStatus OnUpdate() => navAgent.remainingDistance < Mathf.Epsilon ? ExecutionStatus.Success : ExecutionStatus.Running;
    }
}
using Platinio.GraphCore;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Stop NavAgent")]
    public class StopNavAgent : GameplayNode
    {
        public override string NodeName => "Stop NavAgent";

        private NavMeshAgent navAgent = null;

        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }
    }
}
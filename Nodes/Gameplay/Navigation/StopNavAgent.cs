using Platinio.GraphCore;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Stop NavAgent")]
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
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }
    }
}
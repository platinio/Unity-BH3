using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Set NavAgent Position")]
    public class SetNavAgentPosition : GameplayNode
    {
        [Serialize, Inspectable] private string NavPositionVariableName;
        [Serialize, Inspectable] private bool ClearCurrentPath = false;
       
        public override string NodeName => "Set Nav Agent Position";

        private NavMeshAgent navAgent = null;

        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;

            navAgent.isStopped = false;
            if (ClearCurrentPath) navAgent.ResetPath();
            TryUpdateNavAgentPosition();
        }

        public override ExecutionStatus OnUpdate()
        {
            if (navAgent.pathPending) return ExecutionStatus.Running;
            if (navAgent.pathStatus == NavMeshPathStatus.PathComplete) return ExecutionStatus.Success;

            return ExecutionStatus.Failure;
        }

        public override void OnExit()
        {
           
        }

        private void TryUpdateNavAgentPosition()
        {
            if (!Machine.Variables.declarations.IsDefined(NavPositionVariableName)) return;
            
            Vector3 pos = Machine.Variables.declarations.Get<Vector3>(NavPositionVariableName);
            navAgent.SetDestination(pos);
        }
    }
}


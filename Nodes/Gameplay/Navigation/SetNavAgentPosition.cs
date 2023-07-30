using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Set NavAgent Position")]
    public class SetNavAgentPosition : GameplayNode
    {
        [Serialize] [Inspectable] private string NavPositionVariableName;
       
        public override string NodeName => "Set Nav Agent Position";

        private NavMeshAgent m_navAgent = null;

        public override void OnAwake()
        {
            m_navAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
            m_navAgent.updatePosition = true;
            m_navAgent.updateRotation = true;

            m_navAgent.isStopped = false;
            m_navAgent.ResetPath();
            TryUpdateNavAgentPosition();
        }

        public override ExecutionStatus OnUpdate()
        {
            if (m_navAgent.pathPending) return ExecutionStatus.Running;
            if (m_navAgent.pathStatus == NavMeshPathStatus.PathComplete) return ExecutionStatus.Success;

            return ExecutionStatus.Failure;
        }

        public override void OnExit()
        {
           
        }

        private void TryUpdateNavAgentPosition()
        {
            if (!Machine.Variables.IsDefined(NavPositionVariableName)) return;
            
            Vector3 pos = Machine.Variables.Get<Vector3>(NavPositionVariableName);
            m_navAgent.SetDestination(pos);
        }
    }
}


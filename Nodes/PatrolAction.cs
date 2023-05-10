using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Create Patrol")]
    public class PatrolAction : GameplayNode
    {
        public override string NodeName => "Patrol";
        protected override string NodeIconPath => "NodeIcons/Patrol";
        private NavMeshAgent m_navMhesAgent;
        
        [Serialize] [Inspectable] private string m_minPatrolDistanceKey = "MinPatrolDistance";
        [Serialize] [Inspectable] private string m_maxPatrolDistanceKey = "MaxPatrolDistance";
        [Serialize] [Inspectable] private string m_distanceToNavTarget = "DistanceToNavTarget";


        public override void OnAwake()
        {
            m_navMhesAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
           SetRandomPatrolPosition();
        }

        private void SetRandomPatrolPosition()
        {
            
            Vector2 direction = Random.insideUnitCircle;
            direction.Normalize();

            float minPatrolDistance = VariableDeclarations.Get<float>(m_minPatrolDistanceKey);
            float maxPatrolDistance = VariableDeclarations.Get<float>(m_maxPatrolDistanceKey);
            float distance = Random.Range(minPatrolDistance, maxPatrolDistance);
            Vector3 patrolPosition = transform.position + (new Vector3(direction.x, 0.0f, direction.y) * distance);

            m_navMhesAgent.isStopped = false;
            m_navMhesAgent.SetDestination(patrolPosition);
        }

        public override ExecutionStatus OnUpdate()
        {
            if (m_navMhesAgent.pathPending) return ExecutionStatus.Running;
            if (m_navMhesAgent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                SetRandomPatrolPosition();
                return ExecutionStatus.Running;
            }

            bool isComplete = Vector3.Distance(m_navMhesAgent.destination, transform.position) < 0.25f;
            return isComplete ? ExecutionStatus.Success : ExecutionStatus.Running;
        }

    }
}
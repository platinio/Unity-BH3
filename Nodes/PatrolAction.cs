using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Create Patrol")]
    public class PatrolAction : GameplayNode
    {
        public override string NodeName => "Patrol";
        protected override string NodeIconPath => "NodeIcons/Patrol";
        private NavMeshAgent navMhesAgent;
        
        [Serialize] [Inspectable] private string minPatrolDistanceKey = "MinPatrolDistance";
        [Serialize] [Inspectable] private string maxPatrolDistanceKey = "MaxPatrolDistance";
        [Serialize] [Inspectable] private string distanceToNavTarget = "DistanceToNavTarget";


        public override void OnAwake()
        {
            navMhesAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
           SetRandomPatrolPosition();
        }

        private void SetRandomPatrolPosition()
        {
            
            Vector2 direction = Random.insideUnitCircle;
            direction.Normalize();

            float minPatrolDistance = VariableDeclarations.Get<float>(minPatrolDistanceKey);
            float maxPatrolDistance = VariableDeclarations.Get<float>(maxPatrolDistanceKey);
            float distance = Random.Range(minPatrolDistance, maxPatrolDistance);
            Vector3 patrolPosition = transform.position + (new Vector3(direction.x, 0.0f, direction.y) * distance);

            navMhesAgent.isStopped = false;
            navMhesAgent.SetDestination(patrolPosition);
        }

        public override ExecutionStatus OnUpdate()
        {
            if (navMhesAgent.pathPending) return ExecutionStatus.Running;
            if (navMhesAgent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                SetRandomPatrolPosition();
                return ExecutionStatus.Running;
            }

            bool isComplete = Vector3.Distance(navMhesAgent.destination, transform.position) < 0.25f;
            return isComplete ? ExecutionStatus.Success : ExecutionStatus.Running;
        }

    }
}
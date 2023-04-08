using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Action/Create Patrol")]
    public class PatrolAction : ActionNode
    {
        public override string NodeName => "Patrol";
        protected override string NodeIconPath => "NodeIcons/Patrol";
        private NavMeshAgent m_navMhesAgent;
        
        [Serialize] [Inspectable]
        private float m_patrolRange = 10;

        [Serialize] [Inspectable]
        private BooleanBlackboardVariable m_test = new BooleanBlackboardVariable();
        
        [Serialize] [Inspectable]
        private float aFloatValue = 10;

        public override void OnAwake()
        {
            m_navMhesAgent = GetComponent<NavMeshAgent>();
            m_patrolRange = 25.0f;
        }

        public override void OnEnter()
        {
            m_navMhesAgent.SetDestination(GetRandomPointInsideNavMesh());
        }

        public override ExecutionStatus OnUpdate()
        {
            if (m_navMhesAgent.pathPending || m_navMhesAgent.pathStatus != NavMeshPathStatus.PathComplete) return ExecutionStatus.Running;

            bool isComplete = Vector3.Distance(m_navMhesAgent.destination, transform.position) < 0.25f;
            return isComplete ? ExecutionStatus.Success : ExecutionStatus.Running;
        }

        private Vector3 GetRandomPointInsideNavMesh()
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0.0f;
            Vector3 position = dir * m_patrolRange;
            return transform.position + position;

        }
    }
}
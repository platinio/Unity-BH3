using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Conditions/Navigation/Has Reached Nav Destination")]
    public class HasReachedNavDestination : Condition
    {
        [Serialize, Inspectable] private float StoppingDistanceOffset = 0.01f;
        
        private NavMeshAgent m_navAgent = null;

        public override string NodeName => "Has Reached Nav Destination";

        public override void OnAwake()
        {
            m_navAgent = GetComponent<NavMeshAgent>();
        }
        
        public override bool Evaluate()
        {
            return Vector3.Distance(transform.position, m_navAgent.destination) < m_navAgent.stoppingDistance + StoppingDistanceOffset;
        }
    }
}
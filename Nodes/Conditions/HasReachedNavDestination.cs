using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Conditions/Navigation/Has Reached Nav Destination")]
    public class HasReachedNavDestination : Condition
    {
        [Serialize, Inspectable] private float StoppingDistanceOffset = 0.01f;
        
        private NavMeshAgent navAgent = null;

        public override string NodeName => "Has Reached Nav Destination";

        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }
        
        public override bool Evaluate()
        {
            return Vector3.Distance(transform.position, navAgent.destination) < navAgent.stoppingDistance + StoppingDistanceOffset;
        }
    }
}
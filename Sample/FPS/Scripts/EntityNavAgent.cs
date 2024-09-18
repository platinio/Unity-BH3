using System;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.Share
{
    public class EntityNavAgent : MonoBehaviour
    {
        private NavMeshAgent navMeshAgent;
        private bool hasDestination = false;
        
        public event Action OnDestinationReached;
        public event Action<Vector3> OnSetNewDestination;

        public NavMeshAgent NavMeshAgent => navMeshAgent;

        private void Start()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }

        private void OnDestroy()
        {
            OnDestinationReached = null;
            OnSetNewDestination = null;
        }

        private void Update()
        {
            if (navMeshAgent.pathPending || !hasDestination) return;

            if (navMeshAgent.remainingDistance < 0.1f)
            {
                hasDestination = false;
                OnDestinationReached?.Invoke();
            }
        }

        public bool SetDestination(Vector3 position)
        {
            hasDestination = true;
            
            OnSetNewDestination?.Invoke(position);
            return navMeshAgent.SetDestination(position);
        }
    }
}
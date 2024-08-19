using UnityEngine;
using UnityEngine.AI;

namespace Platinio.AI
{
    public class AIEntity : GameEntity
    {
        [SerializeField] private NavMeshAgent navMeshAgent;
        
        public void SetDestination(Vector3 target)
        {
            navMeshAgent.SetDestination(target);
        }
    }
}
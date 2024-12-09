using Platinio.SDK.DependencyInjection;
using Platinio.Share;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.Share
{
    public class AIEntity : GameEntity
    {
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private Transform aimTarget;

        public Transform AimTarget => aimTarget;

        private static int avoidanceIndex = 0;
        
        protected virtual void Start()
        {
            var gameEntityDatabaseService = ServicesContainer.Resolve<IDatabaseService<AIEntity>>();
            gameEntityDatabaseService?.Add(this);

            navMeshAgent.avoidancePriority = avoidanceIndex++;
        }

        private void OnDestroy()
        {
            var gameEntityDatabaseService = ServicesContainer.Resolve<IDatabaseService<AIEntity>>();
            gameEntityDatabaseService?.Remove(this);
        }
        
        public void SetDestination(Vector3 target)
        {
            navMeshAgent.SetDestination(target);
        }
    }
}
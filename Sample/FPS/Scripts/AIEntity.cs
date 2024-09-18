using Platinio.SDK.DependencyInjection;
using Platinio.Share;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.Share
{
    public class AIEntity : GameEntity
    {
        [SerializeField] private NavMeshAgent navMeshAgent;
        
        protected virtual void Start()
        {
            var gameEntityDatabaseService = ServicesContainer.Resolve<IDatabaseService<AIEntity>>();
            gameEntityDatabaseService?.Add(this);
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
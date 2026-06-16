using ArcaneOnyx.BehaviorTree.Sample;
using ArcaneOnyx.Services;
// TODO: ServiceLocator submodule removed — restore entity registration via the UnityExtensions service registry.
// using ArcaneOnyx.ServiceLocator;
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

        private DamageableManager damageableManager = null;
        
        protected virtual void Start()
        {
            damageableManager = GetComponent<DamageableManager>();
            
            // TODO: ServiceLocator removed — re-register this entity once service resolution is restored.
            // var gameEntityDatabaseService = ServicesContainer.Resolve<IDatabaseService<AIEntity>>();
            // gameEntityDatabaseService?.Add(this);

            navMeshAgent.avoidancePriority = avoidanceIndex++;
        }

        public bool IsAlive()
        {
            if (damageableManager == null) return false;
            return damageableManager.HP > 0;
        }

        private void OnDestroy()
        {
            // TODO: ServiceLocator removed — unregister this entity once service resolution is restored.
            // var gameEntityDatabaseService = ServicesContainer.Resolve<IDatabaseService<AIEntity>>();
            // gameEntityDatabaseService?.Remove(this);
        }
        
        public void SetDestination(Vector3 target)
        {
            navMeshAgent.SetDestination(target);
        }
    }
}

using ArcaneOnyx.BehaviorTree.Sample;
using Platinio.SDK.DependencyInjection;
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
            
            var gameEntityDatabaseService = ServicesContainer.Resolve<IDatabaseService<AIEntity>>();
            gameEntityDatabaseService?.Add(this);

            navMeshAgent.avoidancePriority = avoidanceIndex++;
        }

        public bool IsAlive()
        {
            if (damageableManager == null) return false;
            return damageableManager.HP > 0;
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
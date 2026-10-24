using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(NavMeshAgent), typeof(AgentVariableWriter))]
    public sealed class Slowed : MonoBehaviour
    {
        private NavMeshAgent navAgent;
        private AgentVariableWriter facts;
        private float fullSpeed;
        private float slowedUntil;

        public bool IsActive => Time.time < slowedUntil;

        private void Start()
        {
            navAgent = GetComponent<NavMeshAgent>();
            facts = AgentVariableWriter.On(gameObject);
            fullSpeed = navAgent.speed;
        }

        public void Apply(float seconds, float speedFactor)
        {
            if (navAgent == null) Start();

            slowedUntil = Time.time + seconds;
            navAgent.speed = fullSpeed * speedFactor;
            facts.Write(this, CombatFacts.Slowed, true);
        }

        private void Update()
        {
            if (IsActive || navAgent.speed == fullSpeed) return;

            navAgent.speed = fullSpeed;
            facts.Write(this, CombatFacts.Slowed, false);
        }
    }
}

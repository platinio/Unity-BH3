using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Combatant), typeof(AgentVariableWriter))]
    public sealed class Sight : MonoBehaviour
    {
        [SerializeField] private float radius = 8f;
        [SerializeField] private float scanInterval = 0.2f;

        private Combatant self;
        private AgentVariableWriter facts;
        private float nextScanTime;

        private void Awake()
        {
            self = GetComponent<Combatant>();
            facts = AgentVariableWriter.On(gameObject);
        }

        private void Update()
        {
            if (!self.IsAlive || Time.time < nextScanTime) return;
            nextScanTime = Time.time + scanInterval;

            Combatant seen = NearestVisibleEnemy();
            facts.Write(this, CombatFacts.HasTarget, seen != null);
            if (seen != null) facts.Write(this, CombatFacts.Target, seen.gameObject);
        }

        private Combatant NearestVisibleEnemy()
        {
            Combatant nearest = null;
            float nearestDistance = radius;

            foreach (Combatant other in Combatant.All)
            {
                if (!self.IsEnemyOf(other) || !other.IsAlive) continue;

                float distance = Vector3.Distance(transform.position, other.transform.position);
                if (distance > nearestDistance || !CanSee(other)) continue;

                nearest = other;
                nearestDistance = distance;
            }

            return nearest;
        }

        private bool CanSee(Combatant other)
        {
            Vector3 toOther = other.EyePosition - self.EyePosition;
            bool somethingInBetween = Physics.Raycast(self.EyePosition, toOther.normalized, out RaycastHit hit, toOther.magnitude);

            return !somethingInBetween || hit.transform.IsChildOf(other.transform);
        }
    }
}

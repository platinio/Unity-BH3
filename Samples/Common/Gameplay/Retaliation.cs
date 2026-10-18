using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Health), typeof(Combatant), typeof(AgentVariableWriter))]
    public sealed class Retaliation : MonoBehaviour
    {
        private Health health;
        private Combatant self;
        private AgentVariableWriter facts;

        private void Awake()
        {
            health = GetComponent<Health>();
            self = GetComponent<Combatant>();
            facts = AgentVariableWriter.On(gameObject);
        }

        private void OnEnable() => health.Damaged += FightBackAgainst;

        private void OnDisable() => health.Damaged -= FightBackAgainst;

        private void FightBackAgainst(GameObject attacker)
        {
            if (HasTarget || !self.IsAlive || attacker == null) return;
            if (!attacker.TryGetComponent(out Combatant enemy) || !self.IsEnemyOf(enemy) || !enemy.IsAlive) return;

            facts.Write(this, CombatFacts.Target, enemy.gameObject);
            facts.Write(this, CombatFacts.HasTarget, true);
        }

        private bool HasTarget => facts.Read(CombatFacts.HasTarget) is true;
    }
}

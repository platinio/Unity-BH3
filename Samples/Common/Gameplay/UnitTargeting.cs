using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class UnitTargeting : MonoBehaviour
    {
        private AgentVariableWriter facts;

        private void Awake() => facts = AgentVariableWriter.On(gameObject);

        private void Update()
        {
            if (facts.Read(CombatFacts.HasTarget) is not true) return;
            if (IsStillAlive(facts.Read(CombatFacts.Target) as GameObject)) return;

            facts.Write(this, CombatFacts.HasTarget, false);
        }

        private static bool IsStillAlive(GameObject target) =>
            target != null && target.TryGetComponent(out Combatant combatant) && combatant.IsAlive;
    }
}

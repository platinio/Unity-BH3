using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    public static class CombatantKindApplier
    {
        private const float AgentHeight = 2f;
        private const float AgentRadius = 0.4f;

        public static void Apply(GameObject agent, ICombatantKind kind)
        {
            agent.GetComponent<BehaviorTreeMachine>().nest.macro = kind.Tree;
            agent.GetComponent<Health>().SetMaxHealth(kind.MaxHealth);
            if (agent.TryGetComponent(out Weapon weapon) && kind.Weapon != null) weapon.Equip(kind.Weapon);
            if (agent.TryGetComponent(out Sight sight)) sight.Radius = kind.SightRadius;
            SizeAgent(agent, kind);
            DressBody(agent, kind);
            AgentVariableWriter.On(agent).Write(nameof(CombatantKindApplier), CombatFacts.KeepDistance, kind.KeepDistance);
        }

        private static void SizeAgent(GameObject agent, ICombatantKind kind)
        {
            NavMeshAgent navAgent = agent.GetComponent<NavMeshAgent>();
            navAgent.speed = kind.Speed;
            navAgent.height = AgentHeight * kind.BodyScale;
            navAgent.radius = AgentRadius * kind.BodyScale;
        }

        private static void DressBody(GameObject agent, ICombatantKind kind)
        {
            Unit unit = Unit.Of(agent);
            unit.Resize(kind.BodyScale);
            if (kind.BodyMaterial != null) unit.Dress(kind.BodyMaterial);
        }
    }
}

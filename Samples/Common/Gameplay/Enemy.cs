using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Combatant))]
    public sealed class Enemy : MonoBehaviour
    {
        private const float AgentHeight = 2f;
        private const float AgentRadius = 0.4f;

        [SerializeField] private EnemyKind kind;

        public EnemyKind Kind => kind;

        private void Awake()
        {
            if (kind != null) Apply();
        }

        public void Apply(EnemyKind newKind)
        {
            kind = newKind;
            Apply();
        }

        public void Apply()
        {
            GetComponent<BehaviorTreeMachine>().nest.macro = kind.Tree;
            GetComponent<Health>().SetMaxHealth(kind.MaxHealth);
            if (TryGetComponent(out Weapon weapon) && kind.Weapon != null) weapon.Equip(kind.Weapon);
            if (TryGetComponent(out Sight sight)) sight.Radius = kind.SightRadius;
            SizeAgent();
            DressBody();
            AgentVariableWriter.On(gameObject).Write(this, CombatFacts.KeepDistance, kind.KeepDistance);
        }

        private void SizeAgent()
        {
            NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
            navAgent.speed = kind.Speed;
            navAgent.height = AgentHeight * kind.BodyScale;
            navAgent.radius = AgentRadius * kind.BodyScale;
        }

        private void DressBody()
        {
            Unit unit = Unit.Of(gameObject);
            unit.Resize(kind.BodyScale);
            if (kind.BodyMaterial != null) unit.Dress(kind.BodyMaterial);
        }
    }
}

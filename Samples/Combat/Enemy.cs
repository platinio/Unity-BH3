using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Combatant), typeof(Weapon), typeof(Sight))]
    public sealed class Enemy : MonoBehaviour
    {
        private const float CapsuleHeight = 2f;
        private const float CapsuleRadius = 0.4f;

        [SerializeField] private EnemyKind kind;

        public EnemyKind Kind => kind;

        private void Awake() => Apply();

        public void Apply()
        {
            GetComponent<BehaviorTreeMachine>().nest.macro = kind.Tree;
            GetComponent<Health>().SetMaxHealth(kind.MaxHealth);
            GetComponent<Weapon>().Equip(kind.Weapon);
            GetComponent<Sight>().Radius = kind.SightRadius;
            SizeAgent();
            DressBody();
            AgentVariableWriter.On(gameObject).Write(this, CombatFacts.KeepDistance, kind.KeepDistance);
        }

        private void SizeAgent()
        {
            NavMeshAgent navAgent = GetComponent<NavMeshAgent>();
            navAgent.speed = kind.Speed;
            navAgent.height = CapsuleHeight * kind.BodyScale;
            navAgent.radius = CapsuleRadius * kind.BodyScale;
        }

        private void DressBody()
        {
            Transform body = AgentBody.Find(gameObject);
            body.localScale = Vector3.one * kind.BodyScale;
            body.localPosition = Vector3.up * kind.BodyScale;
            AgentBody.FindRenderer(gameObject).sharedMaterial = kind.BodyMaterial;
        }
    }
}

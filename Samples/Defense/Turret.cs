using ArcaneOnyx.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Combatant), typeof(Weapon), typeof(Sight))]
    public sealed class Turret : MonoBehaviour
    {
        [SerializeField] private TurretKind kind;

        public TurretKind Kind => kind;

        private void Awake()
        {
            if (kind != null) Apply();
        }

        public void Apply(TurretKind newKind)
        {
            kind = newKind;
            Apply();
        }

        public void Apply()
        {
            GetComponent<BehaviorTreeMachine>().nest.macro = kind.Tree;
            GetComponent<Weapon>().Equip(kind.Weapon);
            GetComponent<Sight>().Radius = kind.Weapon.Range;
            GetComponent<NavMeshAgent>().enabled = false;
            AgentBody.FindRenderer(gameObject).sharedMaterial = kind.BodyMaterial;
        }
    }
}

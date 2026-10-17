using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Combatant))]
    public sealed class Enemy : MonoBehaviour
    {
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

        public void Apply() => CombatantKindApplier.Apply(gameObject, kind);
    }
}

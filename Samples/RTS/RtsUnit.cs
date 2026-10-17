using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Combatant))]
    public sealed class RtsUnit : MonoBehaviour
    {
        [SerializeField] private UnitKind kind;

        public UnitKind Kind => kind;

        private void Awake()
        {
            if (kind != null) Apply();
        }

        public void Apply(UnitKind newKind)
        {
            kind = newKind;
            Apply();
        }

        public void Apply() => CombatantKindApplier.Apply(gameObject, kind);
    }
}

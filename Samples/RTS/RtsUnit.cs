using ArcaneOnyx.BehaviorTree;
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

        private void Start() => PublishNearestDepot();

        public void Apply(UnitKind newKind)
        {
            kind = newKind;
            Apply();
        }

        public void Apply() => CombatantKindApplier.Apply(gameObject, kind);

        private void PublishNearestDepot()
        {
            Depot depot = Depot.Nearest(transform.position);
            if (depot != null) AgentVariableWriter.On(gameObject).Write(this, RtsFacts.Depot, depot.gameObject);
        }
    }
}

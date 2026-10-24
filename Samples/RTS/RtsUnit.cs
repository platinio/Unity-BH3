using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Combatant))]
    public sealed class RtsUnit : MonoBehaviour
    {
        [SerializeField] private UnitKind kind;

        public UnitKind Kind => kind;

        private void Start() => PublishNearestDepot();

        private void PublishNearestDepot()
        {
            Depot depot = Depot.Nearest(transform.position);
            if (depot != null) AgentVariableWriter.On(gameObject).Write(this, RtsFacts.Depot, depot.gameObject);
        }
    }
}

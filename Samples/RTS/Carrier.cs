using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class Carrier : MonoBehaviour
    {
        [SerializeField] private int capacity = 20;

        private AgentVariableWriter facts;
        private Resource kind;
        private int amount;

        public Resource Kind => kind;
        public int Amount => amount;
        public int SpaceLeft => capacity - amount;
        public bool IsFull => amount >= capacity;
        public bool IsCarryingSomething => amount > 0;

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            Publish();
        }

        public bool CanTake(Resource offered) => !IsFull && (!IsCarryingSomething || kind == offered);

        public void Take(Resource taken, int count)
        {
            if (count <= 0 || !CanTake(taken)) return;

            kind = taken;
            amount = Mathf.Min(capacity, amount + count);
            Publish();
        }

        public void Empty()
        {
            amount = 0;
            Publish();
        }

        private void Publish()
        {
            facts.Write(this, RtsFacts.CarriedKind, kind.ToString());
            facts.Write(this, RtsFacts.CarriedAmount, amount);
            facts.Write(this, RtsFacts.IsCarryingFull, IsFull);
        }
    }
}

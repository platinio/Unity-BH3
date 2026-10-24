using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/RTS/Deposit")]
    public class Deposit : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Depot { get; private set; }

        public override string NodeName => "Deposit";
        public override string Description => "Hands everything the agent's Carrier holds to Depot. Fails when the carrier is empty or Depot is not one.";

        protected override void Definition()
        {
            base.Definition();

            Depot = ValueInput<GameObject>(nameof(Depot));
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Carrier carrier = ctx.GetComponent<Carrier>();
            GameObject target = ctx.GetValue<GameObject>(Depot);
            if (carrier == null || !carrier.IsCarryingSomething || target == null) return ExecutionStatus.Failure;
            if (!target.TryGetComponent(out Depot depot)) return ExecutionStatus.Failure;

            depot.Receive(carrier);
            return ExecutionStatus.Success;
        }
    }
}

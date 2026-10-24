using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/RTS/Harvest")]
    public class Harvest : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Resource { get; private set; }

        public override string NodeName => "Harvest";
        public override string Description => "Takes one swing's worth from Resource into the agent's Carrier. Fails when the carrier is full or holds another kind, or when the node is empty.";

        protected override void Definition()
        {
            base.Definition();

            Resource = ValueInput<GameObject>(nameof(Resource));
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Carrier carrier = ctx.GetComponent<Carrier>();
            GameObject target = ctx.GetValue<GameObject>(Resource);
            if (carrier == null || target == null || !target.TryGetComponent(out ResourceNode node)) return ExecutionStatus.Failure;
            if (node.IsEmpty || !carrier.CanTake(node.Kind)) return ExecutionStatus.Failure;

            carrier.Take(node.Kind, node.Harvest(carrier.SpaceLeft));
            return ExecutionStatus.Success;
        }
    }
}

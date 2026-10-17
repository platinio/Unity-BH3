using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Combat/Apply Pickup")]
    public class ApplyPickup : GameplayNode
    {
        public override string NodeName => "Apply Pickup";
        public override string Description => "Runs the pickup's effect Function on the player standing in reach. Fails when nobody is there or the effect refuses.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Pickup pickup = ctx.GetComponent<Pickup>();
            if (pickup == null || !pickup.Apply()) return ExecutionStatus.Failure;

            return ExecutionStatus.Success;
        }
    }
}

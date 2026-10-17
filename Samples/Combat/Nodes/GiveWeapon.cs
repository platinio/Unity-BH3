using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Combat/Give Weapon")]
    public class GiveWeapon : GameplayNode
    {
        public override string NodeName => "Give Weapon";
        public override string Description => "Hands the chest's weapon to the player standing in reach. Fails when nobody is there.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Chest chest = ctx.GetComponent<Chest>();
            if (chest == null || !chest.GiveWeapon()) return ExecutionStatus.Failure;

            return ExecutionStatus.Success;
        }
    }
}

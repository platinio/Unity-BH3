using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Combat/Respawn")]
    public class Respawn : GameplayNode
    {
        public override string NodeName => "Respawn";
        public override string Description => "Puts the agent back at its spawn point with full health and its body restored.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Combatant combatant = ctx.GetComponent<Combatant>();
            if (combatant == null) return ExecutionStatus.Failure;

            combatant.Respawn();
            return ExecutionStatus.Success;
        }
    }
}

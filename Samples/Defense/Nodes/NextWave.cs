using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Defense/Next Wave")]
    public class NextWave : GameplayNode
    {
        public override string NodeName => "Next Wave";
        public override string Description => "Advances the game to the next wave.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            DefenseGame game = ctx.GetComponent<DefenseGame>();
            if (game == null) return ExecutionStatus.Failure;

            game.NextWave();
            return ExecutionStatus.Success;
        }
    }
}

using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Defense/Breach Base")]
    public class BreachBase : GameplayNode
    {
        public override string NodeName => "Breach Base";
        public override string Description => "Costs the defender one life: the walker made it to the end of the path.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Walker walker = ctx.GetComponent<Walker>();
            if (walker == null) return ExecutionStatus.Failure;

            walker.Breach();
            return ExecutionStatus.Success;
        }
    }
}

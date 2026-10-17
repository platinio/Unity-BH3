using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Defense/Advance Waypoint")]
    public class AdvanceWaypoint : GameplayNode
    {
        public override string NodeName => "Advance Waypoint";
        public override string Description => "Moves the walker's next waypoint one step along its route; the last step publishes reachedEnd.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Walker walker = ctx.GetComponent<Walker>();
            if (walker == null) return ExecutionStatus.Failure;

            walker.Advance();
            return ExecutionStatus.Success;
        }
    }
}

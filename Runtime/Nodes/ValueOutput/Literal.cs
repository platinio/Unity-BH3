using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    public class Literal : GameplayNode
    {
        public override bool DrawInSubTree => false;

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Failure;
    }
}
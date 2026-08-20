using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public class Literal : GameplayNode
    {
        [Serialize, Inspectable] protected string NodeComment;
        
        public override bool DrawInSubTree => true;
        public override int MaxChildrenLimit => 0;
        public override bool CanBeUsedAsTransitionDestination => false;
        public override bool CanBeUsedAsTransitionSource => false;

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Failure;
    }
}
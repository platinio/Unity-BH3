using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public class Literal : GameplayNode
    {
        [Serialize, Inspectable] protected string VariableName;
        
        public override bool DrawInSubTree => false;
        public override int MaxChildrenLimit => 0;
        public override bool CanBeUseAsTransitionDestination => false;
        public override bool CanBeUseAsTransitionSource => false;

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Failure;
    }
}
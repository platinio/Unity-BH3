using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Remove Key From Blackboard")]
    public class RemoveKeyFromBlackboard : GameplayNode
    {
        [Serialize] [Inspectable] private string key;

        public override string NodeName => "Remove Key";

        public override void OnEnter()
        {
            Variables.declarations.Set(key, false);
        }

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Success;
    }
}


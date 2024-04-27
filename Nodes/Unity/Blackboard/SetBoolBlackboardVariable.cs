using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Blackboard/Set Bool Blackboard Variable")]
    public class SetBoolBlackboardVariable : GameplayNode
    {
        [Serialize] [Inspectable] private BlackboardKey blackboardKey = new();
        [Serialize] [Inspectable] private bool value;

        public override string NodeName => "Set Bool Value";

        public override void OnEnter()
        {
            Variables.declarations.Set(blackboardKey.BlackboardKeyName, value);
        }

        public override ExecutionStatus OnUpdate() => ExecutionStatus.Success;
    }
}


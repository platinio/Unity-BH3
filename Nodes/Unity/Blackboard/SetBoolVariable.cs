using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Set/Bool Variable")]
    public class SetBoolVariable : GameplayNode
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


using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Flow/Wait Until")]
    public class WaitUntil : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => "Wait Until";
        public override string Description => "Runs until Value is true, then succeeds.";

        protected override void Definition()
        {
            base.Definition();

            Value = ValueInput<bool>(nameof(Value), false);
        }

        public override ExecutionStatus OnUpdate() =>
            Value.GetValue<bool>() ? ExecutionStatus.Success : ExecutionStatus.Running;
    }
}

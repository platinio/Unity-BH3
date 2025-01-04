using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Add Conditional Execution/Boolean Conditional")]
    public class BooleanConditionalExecution : ConditionalExecution
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => "Boolean Conditional Execution";
        public override string Description => "Only executes the node if the input boolean value is true";

        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueInput<bool>(nameof(Value), false);
        }

        public override bool Evaluate() => (bool)Value.GetValue();
    }
}
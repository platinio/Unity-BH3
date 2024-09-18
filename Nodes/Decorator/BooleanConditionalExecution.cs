using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Add Conditional Execution/Boolean Conditional")]
    public class BooleanConditionalExecution : ConditionalExecution
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => "Boolean Conditional Execution";

        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueInput<bool>(nameof(Value));
        }

        public override bool Evaluate() => (bool)Value.GetValue();
    }
}
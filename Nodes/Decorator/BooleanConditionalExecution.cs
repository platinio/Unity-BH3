using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public class BooleanConditionalExecution : ConditionalExecution
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }
        
        public BooleanConditionalExecution(BehaviorTreeNode owner) : base(owner)
        {
        }

        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueInput<bool>(nameof(Value));
        }

        public override bool Evaluate() => (bool)Value.GetValue();
    }
}
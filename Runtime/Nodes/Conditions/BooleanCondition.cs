using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The condition counterpart of <see cref="BooleanConditionalExecution"/>. It turns any boolean value
    /// port into a decision inside a Selector, so a value node never has to know it is being used as a test.
    /// </summary>
    [GraphCreateMenu("Condition/Boolean Condition")]
    public class BooleanCondition : Condition
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => "Boolean Condition";

        public override string Description => "Returns SUCCESS while the input boolean is true, FAILURE otherwise.";

        protected override void Definition()
        {
            base.Definition();

            Value = ValueInput<bool>(nameof(Value), false);
        }

        public override bool Evaluate() => Value.GetValue<bool>();
    }
}

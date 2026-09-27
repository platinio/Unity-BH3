using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Convert/To String")]
    public class ObjectToString : Literal
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        [DoNotSerialize]
        public ValueOutput Result { get; private set; }

        public override string NodeName => string.IsNullOrEmpty(NodeComment)? "To String" : NodeComment;

        public override string Description => "Outputs Value as text, so any output can feed a string port.";

        protected override void Definition()
        {
            base.Definition();

            Value = ValueInput<object>(nameof(Value));
            Result = ValueOutput<string>(nameof(Result), () => Value.GetValue()?.ToString() ?? string.Empty);
        }
    }
}

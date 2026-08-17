using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/Integer")]
    public class IntegerLiteral : Literal
    {
        // int, not float. The port below declares int, and GetValue hands consumers the raw boxed object --
        // so a float in here made the idiomatic (int) port.GetValue() throw InvalidCastException at every
        // consumer. The port type is a promise; this is the field that has to keep it.
        [Serialize, Inspectable] private int value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => string.IsNullOrEmpty(NodeComment)? "Integer Literal" : NodeComment;
        public override bool CanBeUsedAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<int>(nameof(Value), () => value);
        }
    }
}
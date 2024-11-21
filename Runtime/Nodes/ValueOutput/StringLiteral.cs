using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/String")]
    public class StringLiteral : Literal
    {
        [Serialize, Inspectable] private string value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => string.IsNullOrEmpty(VariableName)? "String Literal" : VariableName;
        public override bool CanBeUseAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<string>(nameof(Value), () => value);
        }
    }
}
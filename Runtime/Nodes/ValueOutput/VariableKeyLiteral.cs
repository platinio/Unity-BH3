using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/Variable Key")]
    public class VariableKeyLiteral : Literal
    {
        [Serialize, Inspectable] private BlackboardKey Key = new();
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => "Variable Key Literal";
        public override bool CanBeUseAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<string>(nameof(Value), () => Key.BlackboardKeyName);
        }
        
    }
}
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Literal/Boolean")]
    public class BoolLiteral : Literal
    {
        [Serialize, Inspectable] private bool value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }
        
        public override bool CanBeUsedAsTransitionDestination => false;

        public override string NodeName => string.IsNullOrEmpty(NodeComment)? "Boolean Literal" : NodeComment;
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<bool>(nameof(Value), () => value);
        }
        
    }
}
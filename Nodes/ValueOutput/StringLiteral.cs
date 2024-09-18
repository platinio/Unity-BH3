using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/String")]
    public class StringLiteral : GameplayNode
    {
        [Serialize, Inspectable] private string value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => "String Literal";
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<string>(nameof(Value), () => value);
        }
    }
}
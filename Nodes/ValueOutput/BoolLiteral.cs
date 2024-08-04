using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/Boolean")]
    public class BoolLiteral : GameplayNode
    {
        [Serialize, Inspectable] private bool value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => "Boolean Literal";
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<bool>(nameof(Value), () => value);
        }
        
    }
}
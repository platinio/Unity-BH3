using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/Float")]
    public class FloatLiteral : GameplayNode
    {
        [Serialize, Inspectable] private float value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => "Float Literal";
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<float>(nameof(Value), () => value);
        }
    }
}
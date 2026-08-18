using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Math/Modulo")]
    public class Modulo : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput A { get; private set; }
        
        [DoNotSerialize]
        public ValueInput B { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Result { get; private set; }
        
        public override string NodeName => "A % B";
        public override bool CanBeUsedAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
           
            A = ValueInput<float>(nameof(A), 0.0f);
            B = ValueInput<float>(nameof(B), 0.0f);
            
            Result = ValueOutput<float>(nameof(Result), () =>
            {
                float a = A.GetValue<float>();
                float b = B.GetValue<float>();
                
                return a % b;
            });
        }
    }
}
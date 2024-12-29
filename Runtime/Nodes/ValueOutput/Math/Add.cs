using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Math/Sum")]
    public class Sum : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput A { get; private set; }
        
        [DoNotSerialize]
        public ValueInput B { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Result { get; private set; }
        
        public override string NodeName => "Sum";
        public override bool CanBeUseAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
           
            A = ValueInput<float>(nameof(A), 0.0f);
            B = ValueInput<float>(nameof(B), 0.0f);
            
            Result = ValueOutput<GameObject>(nameof(Result), () =>
            {
                float a = (float) A.GetValue();
                float b = (float) B.GetValue();
                
                return a + b;
            });
        }
    }
}
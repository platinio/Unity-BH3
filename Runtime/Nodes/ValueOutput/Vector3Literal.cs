using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/Vector3")]
    public class Vector3Literal : Literal
    {
        [Serialize, Inspectable] private Vector3 value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => "Vector3 Literal";
        public override bool CanBeUseAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<Vector3>(nameof(Value), () => value);
        }
    }
}
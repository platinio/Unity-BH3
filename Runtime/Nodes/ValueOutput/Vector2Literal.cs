using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/Vector2")]
    public class Vector2Literal : Literal
    {
        [Serialize, Inspectable] private Vector3 value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => "Vector2 Literal";
        public override bool CanBeUseAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<Vector2>(nameof(Value), () => value);
        }
    }
}
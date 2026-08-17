using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/Vector2")]
    public class Vector2Literal : Literal
    {
        // Vector2, not Vector3. The implicit Vector3 -> Vector2 conversion does not survive boxing:
        // (Vector2)(object)aVector3 throws InvalidCastException, so every consumer of this port threw.
        // Copy-pasted from Vector3Literal, which also left a dead Z component in the inspector.
        [Serialize, Inspectable] private Vector2 value;
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => string.IsNullOrEmpty(NodeComment)? "Vector2 Literal" : NodeComment;
        public override bool CanBeUsedAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
            
            Value = ValueOutput<Vector2>(nameof(Value), () => value);
        }
    }
}
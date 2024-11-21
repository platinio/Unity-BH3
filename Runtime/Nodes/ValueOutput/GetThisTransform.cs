using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/This/Transform")]
    public class GetThisTransform : Literal
    {
        public override string NodeName => string.IsNullOrEmpty(NodeComment)? "Transform" : NodeComment;

        [DoNotSerialize]
        public ValueOutput thisValueOutput { get; private set; }
        
        protected override void Definition()
        {
            base.Definition();
            thisValueOutput = ValueOutput(typeof(Transform), "", () => transform);
        }
    }
}
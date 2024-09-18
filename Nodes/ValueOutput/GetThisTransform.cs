using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/This/Transform")]
    public class GetThisTransform : GameplayNode
    {
        public override string NodeName => "Transform";

        [DoNotSerialize]
        public ValueOutput thisValueOutput { get; private set; }
        
        protected override void Definition()
        {
            base.Definition();
            thisValueOutput = ValueOutput(typeof(Transform), "", () => transform);
        }
    }
}
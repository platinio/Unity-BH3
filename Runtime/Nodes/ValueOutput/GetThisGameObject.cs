using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/This/GameObject")]
    public class GetThisGameObject : Literal
    {
        public override string NodeName => string.IsNullOrEmpty(NodeComment)? "GameObject" : NodeComment;

        [DoNotSerialize]
        public ValueOutput thisValueOutput { get; private set; }
        public override bool CanBeUsedAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
            thisValueOutput = ValueOutput(typeof(GameObject), "", () => gameObject);
        }
    }
}


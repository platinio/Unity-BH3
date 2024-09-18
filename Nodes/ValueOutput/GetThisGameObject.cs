using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Literal/This/GameObject")]
    public class GetThisGameObject : GameplayNode
    {
        public override string NodeName => "GameObject";

        [DoNotSerialize]
        public ValueOutput thisValueOutput { get; private set; }
        
        protected override void Definition()
        {
            base.Definition();
            thisValueOutput = ValueOutput(typeof(GameObject), "", () => gameObject);
        }
    }
}


using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
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


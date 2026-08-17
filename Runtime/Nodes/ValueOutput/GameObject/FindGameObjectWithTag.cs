using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public class FindGameObjectWithTag : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Tag { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Output { get; private set; }
        
        public override string NodeName => "Find Game Object";
        public override bool CanBeUsedAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
           
            Tag = ValueInput<string>(nameof(Tag), null);
            
            Output = ValueOutput<GameObject>(nameof(Output), () =>
            {
                string tag = Tag.GetValueOrDefault<string>();
                return GameObject.FindWithTag(tag);
            });
        }
    }
}
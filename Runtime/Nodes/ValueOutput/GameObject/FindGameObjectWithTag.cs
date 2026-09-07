using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Game Object/Find Game Object With Tag")]
    public class FindGameObjectWithTag : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Tag { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Output { get; private set; }
        
        public override string NodeName => "Find Game Object With Tag";
        public override bool CanBeUsedAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
           
            Tag = ValueInput<string>(nameof(Tag), null);
            
            Output = ValueOutput<GameObject>(nameof(Output), () =>
            {
                string tag = Tag.GetValueOrDefault<string>();

                // FindWithTag throws on a null or empty tag where Find just answers null, and this port
                // has no default -- so without this an unconnected node reports a crash instead of a miss.
                if (string.IsNullOrEmpty(tag)) return null;

                return GameObject.FindWithTag(tag);
            });
        }
    }
}
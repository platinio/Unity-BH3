using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Game Object/Find Game Object")]
    public class FindGameObject : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Name { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Output { get; private set; }
        
        public override string NodeName => "Find Game Object";
        public override bool CanBeUsedAsTransitionDestination => false;
        
        protected override void Definition()
        {
            base.Definition();
           
            Name = ValueInput<string>(nameof(Name), null);
            
            Output = ValueOutput<GameObject>(nameof(Output), () =>
            {
                string gameObjectName = Name.GetValueOrDefault<string>();
                return GameObject.Find(gameObjectName);
            });
        }
    }
}
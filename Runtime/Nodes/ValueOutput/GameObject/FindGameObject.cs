using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public class FindGameObject : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput GameObjectName { get; private set; }
        
        public override string NodeName => "Find Game Object";
        
        protected override void Definition()
        {
            base.Definition();
           
            GameObjectName = ValueInput<string>(nameof(GameObjectName), null);
        }
        
        
    }
}
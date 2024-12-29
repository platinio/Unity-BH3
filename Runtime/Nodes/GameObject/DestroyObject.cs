using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public class Destroy : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Object { get; private set; }
        
        protected override void Definition()
        {
            base.Definition();
           
            Object = ValueInput<object>(nameof(Object), null);
        }

        public override ExecutionStatus OnUpdate()
        {
            UnityEngine.Object.Destroy(Object.GetValue() as Object);
            return ExecutionStatus.Success;
        }
    }
}
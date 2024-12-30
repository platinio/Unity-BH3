using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    public class SetRotation : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        [DoNotSerialize]
        public ValueInput NewRotation { get; private set; }

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            NewRotation = ValueInput<Vector3>(nameof(NewRotation), Vector3.zero);
        }

        public override ExecutionStatus OnUpdate()
        {
            var target = GetComponent<Transform>(Target);
            var newPosition = (Vector3) NewRotation.GetValue();

            target.rotation = Quaternion.Euler(newPosition);
            return ExecutionStatus.Success;
        }
    }
}
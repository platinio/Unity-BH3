using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Transform/Set Position")]
    public class SetPosition : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        [DoNotSerialize]
        public ValueInput NewPosition { get; private set; }

        private Transform targetTransform;

        public override string NodeName => "Set Position";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            NewPosition = ValueInput<Vector3>(nameof(NewPosition), Vector3.zero);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            targetTransform = GetComponent<Transform>(Target);
        }

        public override ExecutionStatus OnUpdate()
        {
            var newPosition = NewPosition.GetValue<Vector3>();

            targetTransform.position = newPosition;
            return ExecutionStatus.Success;
        }
    }
}
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Transform/Look At")]
    public class LookAt : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        [DoNotSerialize]
        public ValueInput LookTarget { get; private set; }

        private Transform targetTransform;

        public override string NodeName => "Look At";
        public override string Description => "Rotates the Target to look at the LookTarget.";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            LookTarget = ValueInput<Transform>(nameof(LookTarget), null);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            targetTransform = GetComponent<Transform>(Target);
        }

        public override ExecutionStatus OnUpdate()
        {
            var lookTarget = LookTarget.GetValueOrDefault<Transform>();
            
            targetTransform.LookAt(lookTarget.position, targetTransform.up);
            return ExecutionStatus.Success;
        }
    }
}
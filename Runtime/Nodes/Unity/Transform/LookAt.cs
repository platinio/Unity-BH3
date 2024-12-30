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

        public override string Description => "Rotates the Target to look at the LookTarget.";
        public override string NodeName => "Look At";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            LookTarget = ValueInput<Transform>(nameof(LookTarget), null);
        }

        public override ExecutionStatus OnUpdate()
        {
            var target = GetComponent<Transform>(Target);
            var lookTarget = LookTarget.GetValue() as Transform;
            
            target.LookAt(lookTarget.position, target.up);
            return ExecutionStatus.Success;
        }
    }
}
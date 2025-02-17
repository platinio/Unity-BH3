using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Transform/Rotate")]
    public class SetRotation : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        [DoNotSerialize]
        public ValueInput TargetRotation { get; private set; }
        
        [DoNotSerialize]
        public ValueInput Duration { get; private set; }

        private Quaternion targetRotation;
        private Quaternion fromRotation;
        private Transform targetTransform;
        private float currentTime;
        private float duration;

        public override string NodeName => "Rotate";
        public override string Description => "Rotates the transform to a target rotation in a define duration";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            TargetRotation = ValueInput<Vector3>(nameof(TargetRotation), Vector3.zero);
            Duration = ValueInput<float>(nameof(Duration), 0.0f);
        }
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            targetTransform = GetComponent<Transform>(Target);
            
            var targetRotationEuler = (Vector3) TargetRotation.GetValue();
            targetRotation = Quaternion.Euler(targetRotationEuler);
            
            duration = (float)Duration.GetValue();
            fromRotation = targetTransform.rotation;
            currentTime = 0;
        }

        public override ExecutionStatus OnUpdate()
        {
            targetTransform.rotation = Quaternion.Slerp(fromRotation, targetRotation, currentTime / duration);
            currentTime += Time.deltaTime;

            return currentTime >= duration ? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}
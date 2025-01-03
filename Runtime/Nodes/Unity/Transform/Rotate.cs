using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Transform/Rotate")]
    public class Rotate : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        [DoNotSerialize]
        public ValueInput TargetPosition { get; private set; }
        
        [DoNotSerialize]
        public ValueInput Duration { get; private set; }

        private Vector3 targetPosition;
        private Vector3 fromPosition;
        private Transform targetTransform;
        private float currentTime;
        private float duration;
       
        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            TargetPosition = ValueInput<Vector3>(nameof(TargetPosition), Vector3.zero);
        }
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            targetTransform = GetComponent<Transform>(Target);
            targetPosition = (Vector3) TargetPosition.GetValue();
            duration = (float)Duration.GetValue();
            fromPosition = targetTransform.position;
            currentTime = 0;
        }

        public override ExecutionStatus OnUpdate()
        {
            targetTransform.position = Vector3.Lerp(fromPosition, targetPosition, currentTime / duration);
            currentTime += Time.deltaTime;

            return currentTime >= duration ? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}
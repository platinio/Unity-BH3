using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    // Its own path and its own label. Both used to read "Rotate", the same as the Rotate node -- so one of
    // the two was unreachable in the create menu, and an author looking at an existing tree could not tell
    // which of them an asset actually contained. The class name is deliberately unchanged: that is what
    // assets serialize, so renaming it would break every tree holding one.
    [GraphCreateMenu("Transform/Set Rotation")]
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

        public override string NodeName => "Set Rotation";
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
            
            var targetRotationEuler = TargetRotation.GetValue<Vector3>();
            targetRotation = Quaternion.Euler(targetRotationEuler);
            
            duration = Duration.GetValue<float>();
            fromRotation = targetTransform.rotation;
            currentTime = 0;
        }

        public override ExecutionStatus OnUpdate()
        {
            if (duration <= 0.0f)
            {
                targetTransform.rotation = targetRotation;
                return ExecutionStatus.Success;
            }

            targetTransform.rotation = Quaternion.Slerp(fromRotation, targetRotation, currentTime / duration);
            currentTime += Time.deltaTime;

            return currentTime >= duration ? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}
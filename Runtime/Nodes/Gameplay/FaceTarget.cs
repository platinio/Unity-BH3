using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Face Target")]
    public class FaceTarget : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        [DoNotSerialize]
        public ValueInput TransformTarget { get; private set; }
        [DoNotSerialize]
        public ValueInput RotationOffset { get; private set; }
        [DoNotSerialize]
        public ValueInput RotationSpeed { get; private set; }
        [DoNotSerialize]
        public ValueInput AcceptableRotation { get; private set; }
       
        public override string NodeName => "Face Target";
        public override string Description => "Rotates target transform to face TransformTarget";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<GameObject>(nameof(Target));
            TransformTarget = ValueInput<Transform>(nameof(TransformTarget));
            RotationOffset = ValueInput<Vector3>(nameof(RotationOffset));
            RotationSpeed = ValueInput<float>(nameof(RotationSpeed));
            AcceptableRotation = ValueInput<float>(nameof(AcceptableRotation));
        }

        public override ExecutionStatus OnUpdate()
        {
            FaceTransformTarget();
            
            if (IsFacingTarget()) return ExecutionStatus.Success;
            return ExecutionStatus.Running;
        }

        private bool IsFacingTarget()
        {
            Transform selectedTarget = TransformTarget.GetValue() as Transform;
            if (selectedTarget == null) return true;
            
            Vector3 targetPosition = selectedTarget.position;
            targetPosition.y = transform.position.y;
            Vector3 dir = (targetPosition - transform.position).normalized;
            Vector2 dir2D = new Vector2(dir.x, dir.z).normalized;
            Vector2 thisDir = new Vector2(transform.forward.x, transform.forward.z).normalized;

            float acceptableRotation = (float) AcceptableRotation.GetValue();
            
            return Vector2.Dot(dir2D, thisDir) > acceptableRotation;
        }

        private void FaceTransformTarget()
        {
            Transform selectedTarget = TransformTarget.GetValue() as Transform;
            if (selectedTarget == null) return;

            Vector3 targetPosition = selectedTarget.position;
            targetPosition.y = transform.position.y;
            Vector3 dir = (targetPosition - transform.position).normalized;
            Quaternion desireRot = Quaternion.LookRotation(dir);

            Vector3 rotationOffset = (Vector3) RotationOffset.GetValue();
            float rotationSpeed = (float) RotationSpeed.GetValue();
            
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desireRot * Quaternion.Euler(rotationOffset), rotationSpeed * Time.deltaTime);
        }
    }

}


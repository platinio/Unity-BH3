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
        public ValueInput Speed { get; private set; }
        
        [DoNotSerialize]
        public ValueInput Axis { get; private set; }
       
        private Transform targetTransform;
        private float speed;
        private Vector3 axis;

        public override string NodeName => "Rotate";
        public override string Description => "Rotates the transform to a target rotation in a define duration";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            Speed = ValueInput<float>(nameof(Speed), 0);
        }
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            targetTransform = GetComponent<Transform>(Target);
            speed = (float) Speed.GetValue();
            axis = (Vector3)Axis.GetValue();
        }

        public override ExecutionStatus OnUpdate()
        {
            targetTransform.Rotate(axis, speed);
            return ExecutionStatus.Running;
        }
    }
}
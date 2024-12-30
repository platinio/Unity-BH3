using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Physics/Add Force")]
    public class AddForce : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        [DoNotSerialize]
        public ValueInput Force { get; private set; }

        [Serialize, Inspectable] 
        public ForceMode forceMode;

        public override string NodeName => "Add Force";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            Force = ValueInput<Vector3>(nameof(Force), Vector3.zero);
        }

        public override ExecutionStatus OnUpdate()
        {
            var target = GetComponent<Rigidbody>(Target);
            var force = (Vector3) Force.GetValue();
            
            target.AddForce(force, forceMode);
            return ExecutionStatus.Success;
        }
    }
}
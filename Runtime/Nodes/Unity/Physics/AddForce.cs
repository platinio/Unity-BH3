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
        public override string Description => "Applies physics force to target Rigidbody.";

        private Rigidbody targetRigidbody;

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            Force = ValueInput<Vector3>(nameof(Force), Vector3.zero);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // Resolved per entry rather than once, because Target is a port: what it points at can
            // differ between one entry and the next.
            targetRigidbody = GetComponent<Rigidbody>(Target);
        }

        public override ExecutionStatus OnUpdate()
        {
            var force = Force.GetValue<Vector3>();
            
            targetRigidbody.AddForce(force, forceMode);
            return ExecutionStatus.Success;
        }
    }
}
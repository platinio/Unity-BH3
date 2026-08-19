using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Physics/Add Torque")]
    public class AddTorque : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        [DoNotSerialize]
        public ValueInput Torque { get; private set; }

        [Serialize, Inspectable] 
        public ForceMode forceMode;

        public override string NodeName => "Add Torque";
        private Rigidbody targetRigidbody;

        public override string Description => "Applies torque to the target Rigidbody.";

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            Torque = ValueInput<Vector3>(nameof(Torque), Vector3.zero);
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
            var torque = Torque.GetValue<Vector3>();
            
            targetRigidbody.AddTorque(torque, forceMode);
            return ExecutionStatus.Success;
        }
    }
}
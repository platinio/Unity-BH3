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

        protected override void Definition()
        {
            base.Definition();
            
            Target = ValueInput<Object>(nameof(Target), null);
            Torque = ValueInput<Vector3>(nameof(Torque), Vector3.zero);
        }

        public override ExecutionStatus OnUpdate()
        {
            var target = GetComponent<Rigidbody>(Target);
            var torque = (Vector3) Torque.GetValue();
            
            target.AddTorque(torque, forceMode);
            return ExecutionStatus.Success;
        }
    }
}
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Physics/Add Force")]
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

            TryResolve(Target, out targetRigidbody);
        }

        public override ExecutionStatus OnUpdate()
        {
            // The miss was reported on entry; this is the "and fails" half of it. Without it a tree
            // dropped onto a prefab with no Rigidbody answers with a NullReferenceException from inside
            // the node, which takes the branch down saying nothing about why.
            if (targetRigidbody == null) return ExecutionStatus.Failure;

            var force = Force.GetValue<Vector3>();

            targetRigidbody.AddForce(force, forceMode);
            return ExecutionStatus.Success;
        }
    }
}
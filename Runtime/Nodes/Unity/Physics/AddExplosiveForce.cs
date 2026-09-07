using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Physics/Add Explosive Force")]
    public class AddExplosiveForce : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        [DoNotSerialize]
        public ValueInput ExplosionOrigin { get; private set; }
        
        [DoNotSerialize]
        public ValueInput ExplosionForce { get; private set; }
        
        [DoNotSerialize]
        public ValueInput ExplosionRadius { get; private set; }
        
        [DoNotSerialize]
        public ValueInput ExplosionUpModifier { get; private set; }

        [Serialize, Inspectable]
        public ForceMode forceMode;

        private Rigidbody targetRigidbody;

        public override string NodeName => "Add Explosive Force";
        public override string Description => "Applies a force that simulates explosion effects to the target RigidBody.";

        protected override void Definition()
        {
            base.Definition();
           
            Target = ValueInput<Object>(nameof(Target), null);
            ExplosionOrigin = ValueInput<Vector3>(nameof(ExplosionOrigin), Vector3.zero);
            ExplosionForce = ValueInput<float>(nameof(ExplosionForce), 0.0f);
            ExplosionRadius = ValueInput<float>(nameof(ExplosionRadius), 0.0f);
            ExplosionUpModifier = ValueInput<float>(nameof(ExplosionUpModifier), 0.0f);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (!TryResolve(Target, out targetRigidbody)) return;

            Vector3 explosionOrigin = ExplosionOrigin.GetValue<Vector3>();
            float explosionForce = ExplosionForce.GetValue<float>();
            float explosionRadius = ExplosionRadius.GetValue<float>();
            float explosionUpModifier = ExplosionUpModifier.GetValue<float>();

            targetRigidbody.AddExplosionForce(
                explosionForce, explosionOrigin, explosionRadius, explosionUpModifier, forceMode);
        }

        /// <summary>
        /// Overridden only to be able to fail. This node does its work in <c>OnEnter</c> and inherited
        /// <c>OnUpdate</c>'s default of <c>Success</c> — so a node that had found no Rigidbody and applied
        /// no force would still have reported that it did, which is worse than the exception it used to
        /// throw, not better.
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            return targetRigidbody == null ? ExecutionStatus.Failure : ExecutionStatus.Success;
        }
    }
}
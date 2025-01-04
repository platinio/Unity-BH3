using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Physics/Add Explosive Force")]
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

            var rb = GetComponent<Rigidbody>(Target);

            Vector3 explosionOrigin = (Vector3) ExplosionOrigin.GetValue();
            float explosionForce = (float) ExplosionForce.GetValue();
            float explosionRadius = (float)ExplosionRadius.GetValue();
            float explosionUpModifier = (float)ExplosionUpModifier.GetValue();
            
            rb.AddExplosionForce(explosionForce, explosionOrigin, explosionRadius, explosionUpModifier, forceMode);
        }
    }
}
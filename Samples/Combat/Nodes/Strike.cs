using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Combat/Strike")]
    public class Strike : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Target { get; private set; }

        public override string NodeName => "Strike";
        public override string Description => "Attacks Target once with the weapon the agent holds: a melee hit or a launched projectile.";

        protected override void Definition()
        {
            base.Definition();

            Target = ValueInput<GameObject>(nameof(Target));
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Weapon weapon = ctx.GetComponent<Weapon>();
            GameObject target = ctx.GetValue<GameObject>(Target);
            if (weapon == null || target == null) return ExecutionStatus.Failure;

            weapon.Attack(target);
            return ExecutionStatus.Success;
        }
    }
}

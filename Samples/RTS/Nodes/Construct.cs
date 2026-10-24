using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/RTS/Construct")]
    public class Construct : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Site { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Seconds { get; private set; }

        public override string NodeName => "Construct";
        public override string Description => "Adds Seconds of work to the building at Site. Fails once the building stands or Site is gone.";

        protected override void Definition()
        {
            base.Definition();

            Site = ValueInput<GameObject>(nameof(Site));
            Seconds = ValueInput<float>(nameof(Seconds), 1f);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            GameObject site = ctx.GetValue<GameObject>(Site);
            if (site == null || !site.TryGetComponent(out Building building) || building.IsBuilt) return ExecutionStatus.Failure;

            building.AddWork(ctx.GetValue<float>(Seconds));
            return ExecutionStatus.Success;
        }
    }
}

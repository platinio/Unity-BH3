using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/RTS/Grow")]
    public class Grow : GameplayNode
    {
        private const float FoundationScale = 0.15f;

        public override string NodeName => "Grow";
        public override string Description => "Scales the building's body with its construction progress, from a low foundation to full size.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Building building = ctx.GetComponent<Building>();
            if (building == null) return ExecutionStatus.Failure;

            Unit unit = Unit.Of(ctx);
            unit.Body.localScale = unit.RestingPose.Scale * Mathf.Lerp(FoundationScale, 1f, building.Progress01);
            return ExecutionStatus.Success;
        }
    }
}

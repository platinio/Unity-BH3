using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Recover")]
    public class Recover : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Recover";
        public override string Description => "Eases the body back to the pose it rested in before any Charge, over Duration seconds.";

        private sealed class Memory
        {
            public Tween Returning;
        }

        protected override void Definition()
        {
            base.Definition();

            Duration = ValueInput<float>(nameof(Duration), 0.2f);
        }

        public override void OnEnter(BTContext ctx) =>
            ctx.Memory<Memory>().Returning = Unit.Of(ctx).ReturnToRestingPose(ctx.GetValue<float>(Duration));

        public override ExecutionStatus OnUpdate(BTContext ctx) =>
            ctx.Memory<Memory>().Returning.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;

        public override void OnExit(BTContext ctx) => ctx.Memory<Memory>().Returning.Kill();
    }
}

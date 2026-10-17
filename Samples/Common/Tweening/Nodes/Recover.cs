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
        public override string Description => "Eases the body and every part back to the pose they rested in before any Charge or Swing Part, over Duration seconds. Interrupted, it snaps them back.";

        private sealed class Memory
        {
            public Unit Unit;
            public Tween Returning;
        }

        protected override void Definition()
        {
            base.Definition();

            Duration = ValueInput<float>(nameof(Duration), 0.2f);
        }

        public override void OnEnter(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Unit = Unit.Of(ctx);
            memory.Returning = memory.Unit.ReturnToRestingPose(ctx.GetValue<float>(Duration));
        }

        public override ExecutionStatus OnUpdate(BTContext ctx) =>
            ctx.Memory<Memory>().Returning.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            bool wasInterrupted = memory.Returning.IsPlaying;
            memory.Returning.Kill();
            if (wasInterrupted) memory.Unit.RestoreRestingPose();
        }
    }
}

using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Punch Scale")]
    public class PunchScale : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Strength { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Punch Scale";
        public override string Description => "Swells the body of the unit by Strength and shrinks it back over Duration seconds.";

        private sealed class Memory
        {
            public Transform Body;
            public Vector3 OriginalScale;
            public Tween Punch;
        }

        protected override void Definition()
        {
            base.Definition();

            Strength = ValueInput<float>(nameof(Strength), 0.3f);
            Duration = ValueInput<float>(nameof(Duration), 0.25f);
        }

        public override void OnEnter(BTContext ctx)
        {
            float strength = ctx.GetValue<float>(Strength);
            float duration = ctx.GetValue<float>(Duration);

            Memory memory = ctx.Memory<Memory>();
            memory.Body = Unit.Of(ctx).Body;
            memory.OriginalScale = memory.Body.localScale;
            memory.Punch = memory.Body.PunchScale(strength, duration);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx) =>
            ctx.Memory<Memory>().Punch.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Punch.Kill();
            memory.Body.localScale = memory.OriginalScale;
        }
    }
}

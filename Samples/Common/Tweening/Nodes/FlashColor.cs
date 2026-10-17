using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Flash Color")]
    public class FlashColor : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Tint { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Flash Color";
        public override string Description => "Tints the body of the unit with Tint and fades back over Duration seconds.";

        private sealed class Memory
        {
            public Unit Unit;
            public Tween Flash;
        }

        protected override void Definition()
        {
            base.Definition();

            Tint = ValueInput<Color>(nameof(Tint), UnityEngine.Color.red);
            Duration = ValueInput<float>(nameof(Duration), 0.25f);
        }

        public override void OnEnter(BTContext ctx)
        {
            Color flashColor = ctx.GetValue<Color>(Tint);
            float duration = ctx.GetValue<float>(Duration);

            Memory memory = ctx.Memory<Memory>();
            memory.Unit = Unit.Of(ctx);
            memory.Unit.RestoreRestingColors();
            memory.Flash = memory.Unit.FlashColor(flashColor, duration);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx) =>
            ctx.Memory<Memory>().Flash.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Flash.Kill();
            memory.Unit.RestoreRestingColors();
        }
    }
}

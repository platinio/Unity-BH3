using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Swing Part")]
    public class SwingPart : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Part { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Axis { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput FromAngle { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput ToAngle { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Swing Part";
        public override string Description => "Rotates the unit's part named Part about its own Axis, from FromAngle to ToAngle degrees over Duration seconds, and leaves it there. Recover brings every part back; an interrupted swing snaps back on its own.";

        private sealed class Memory
        {
            public Unit Unit;
            public Tween Swing;
        }

        protected override void Definition()
        {
            base.Definition();

            Part = ValueInput<string>(nameof(Part), "arm-right");
            Axis = ValueInput<Vector3>(nameof(Axis), Vector3.right);
            FromAngle = ValueInput<float>(nameof(FromAngle), 0f);
            ToAngle = ValueInput<float>(nameof(ToAngle), 90f);
            Duration = ValueInput<float>(nameof(Duration), 0.2f);
        }

        public override void OnEnter(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Unit = Unit.Of(ctx);
            Transform part = memory.Unit.Part(ctx.GetValue<string>(Part));
            memory.Swing = memory.Unit.SwingPart(part, ctx.GetValue<Vector3>(Axis), ctx.GetValue<float>(FromAngle), ctx.GetValue<float>(ToAngle), ctx.GetValue<float>(Duration));
        }

        public override ExecutionStatus OnUpdate(BTContext ctx) =>
            ctx.Memory<Memory>().Swing.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            bool wasInterrupted = memory.Swing.IsPlaying;
            memory.Swing.Kill();
            if (wasInterrupted) memory.Unit.RestoreRestingPose();
        }
    }
}

using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Scale To")]
    public class ScaleTo : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Scale { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Scale To";
        public override string Description => "Scales the body of the agent to Scale times its resting size over Duration seconds and leaves it there.";

        private sealed class Memory
        {
            public Tween Scaling;
        }

        protected override void Definition()
        {
            base.Definition();

            Scale = ValueInput<float>(nameof(Scale), 0f);
            Duration = ValueInput<float>(nameof(Duration), 0.4f);
        }

        public override void OnEnter(BTContext ctx)
        {
            float scale = ctx.GetValue<float>(Scale);
            float duration = ctx.GetValue<float>(Duration);
            Transform body = AgentBody.Find(ctx);

            ctx.Memory<Memory>().Scaling = body.ScaleTo(BodyPose.Of(body).RestingScale * scale, duration);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx) =>
            ctx.Memory<Memory>().Scaling.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;

        public override void OnExit(BTContext ctx) => ctx.Memory<Memory>().Scaling.Kill();
    }
}

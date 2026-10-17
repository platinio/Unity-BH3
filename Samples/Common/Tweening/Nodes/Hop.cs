using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Hop")]
    public class Hop : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Height { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Hop";
        public override string Description => "Lifts the body of the agent by Height and lands it again over Duration seconds.";

        private sealed class Memory
        {
            public Transform Body;
            public Vector3 GroundedPosition;
            public Tween Jump;
        }

        protected override void Definition()
        {
            base.Definition();

            Height = ValueInput<float>(nameof(Height), 0.35f);
            Duration = ValueInput<float>(nameof(Duration), 0.3f);
        }

        public override void OnEnter(BTContext ctx)
        {
            float height = ctx.GetValue<float>(Height);
            float duration = ctx.GetValue<float>(Duration);

            Memory memory = ctx.Memory<Memory>();
            memory.Body = AgentBody.Find(ctx);
            memory.GroundedPosition = memory.Body.localPosition;
            memory.Jump = memory.Body.Hop(height, duration);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx) =>
            ctx.Memory<Memory>().Jump.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Jump.Kill();
            memory.Body.localPosition = memory.GroundedPosition;
        }
    }
}

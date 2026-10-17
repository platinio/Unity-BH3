using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/Tween/Charge")]
    public class Charge : GameplayNode
    {
        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput Target { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput LeanAngle { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput WindUp { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput SlamAngle { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public ValueInput SlamDuration { get; private set; }

        public override string NodeName => "Charge";
        public override string Description => "Pivots the body at its feet: leans LeanAngle degrees away from Target over WindUp seconds, then slams SlamAngle degrees into it. Leaves the body slammed; Recover brings it back.";

        private sealed class Memory
        {
            public Transform Body;
            public BodyPose Pose;
            public Vector3 LeanAxis;
            public float SlamAngle;
            public float SlamDuration;
            public Tween Lean;
            public Tween Slam;
        }

        protected override void Definition()
        {
            base.Definition();

            Target = ValueInput<GameObject>(nameof(Target));
            LeanAngle = ValueInput<float>(nameof(LeanAngle), 20f);
            WindUp = ValueInput<float>(nameof(WindUp), 0.25f);
            SlamAngle = ValueInput<float>(nameof(SlamAngle), 25f);
            SlamDuration = ValueInput<float>(nameof(SlamDuration), 0.1f);
        }

        public override void OnEnter(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Body = AgentBody.Find(ctx);
            memory.Pose = BodyPose.Of(memory.Body);
            memory.LeanAxis = AxisToLeanAwayFrom(ctx, ctx.GetValue<GameObject>(Target));
            memory.SlamAngle = ctx.GetValue<float>(SlamAngle);
            memory.SlamDuration = ctx.GetValue<float>(SlamDuration);
            memory.Slam = null;
            memory.Lean = memory.Body.LeanAbout(memory.Pose, memory.LeanAxis, 0f, -ctx.GetValue<float>(LeanAngle), ctx.GetValue<float>(WindUp), Easing.OutQuad);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            if (memory.Lean.IsPlaying) return ExecutionStatus.Running;

            if (memory.Slam == null) memory.Slam = memory.Body.LeanAbout(memory.Pose, memory.LeanAxis, -ctx.GetValue<float>(LeanAngle), memory.SlamAngle, memory.SlamDuration, Easing.InQuad);

            return memory.Slam.IsPlaying ? ExecutionStatus.Running : ExecutionStatus.Success;
        }

        public override void OnExit(BTContext ctx)
        {
            Memory memory = ctx.Memory<Memory>();
            memory.Lean?.Kill();
            memory.Slam?.Kill();
        }

        private static Vector3 AxisToLeanAwayFrom(BTContext ctx, GameObject target)
        {
            Vector3 towardTarget = target.transform.position - ctx.transform.position;
            towardTarget.y = 0f;
            Vector3 rightOfTarget = Vector3.Cross(Vector3.up, towardTarget.normalized);

            return ctx.transform.InverseTransformDirection(rightOfTarget);
        }
    }
}

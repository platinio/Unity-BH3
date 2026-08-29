using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Wait")]
    public class WaitTime : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Time { get; private set; }

        /// <summary>
        /// What used to be <c>private float timer</c>. Migrated (spec 07 step 3): the countdown is per
        /// agent, so it lives in the agent's memory rather than on a node that may be shared by two hundred
        /// of them. The duration it is seeded from stays on the port, which is shared structure.
        /// </summary>
        private sealed class Memory
        {
            public float Remaining;
        }

        public override string NodeName => "Wait";

        protected override void Definition()
        {
            base.Definition();

            Time = ValueInput<float>(nameof(Time));
        }

        public override void OnEnter(BTContext ctx) => ctx.Memory<Memory>().Remaining = ctx.GetValue<float>(Time);

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();
            memory.Remaining -= UnityEngine.Time.deltaTime;

            return memory.Remaining > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}

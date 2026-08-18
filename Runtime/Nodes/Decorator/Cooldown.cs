using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Gates a branch behind a cooldown time.
    /// </summary>
    [GraphCreateMenu("Decorator/Create Cooldown")]
    public class Cooldown : Decorator
    {
        [DoNotSerialize]
        public ValueInput Duration { get; private set; }

        public override string NodeName => "Cooldown";

        public override string Description =>
            "Runs the child, then blocks it for Duration seconds.\nReturns FAILURE while still cooling down.";

        public override int MaxChildrenLimit => 1;

        private float cooldownEndTime = 0.0f;

        protected override void Definition()
        {
            base.Definition();

            Duration = ValueInput<float>(nameof(Duration), 5.0f);
        }

        public override void OnAwake()
        {
            cooldownEndTime = 0.0f;
        }

        /// <summary>
        /// The recharge gate is here and not in <c>OnEnter</c>, because entry is the tick's job — the child
        /// is never reached while cooling down, so it is never entered either. This decorator is where the
        /// enter-if-not-running rule was first written by hand; <see cref="ContainerNode.TickChild"/> is that
        /// rule, now shared by every container that ticks a child it did not just enter.
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success;
            if (IsCoolingDown) return ExecutionStatus.Failure;

            var task = GetChildren()[0];

            var result = TickChild(task);
            if (result == ExecutionStatus.Running) return ExecutionStatus.Running;

            task.OnNodeExit();
            cooldownEndTime = Time.time + Duration.GetValue<float>();

            return result;
        }

        private bool IsCoolingDown => Time.time < cooldownEndTime;
    }
}

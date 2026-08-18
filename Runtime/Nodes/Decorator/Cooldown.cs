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

        public override void OnEnter()
        {
            if (IsCoolingDown) return;

            base.OnEnter();
        }
     
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success;
            if (IsCoolingDown) return ExecutionStatus.Failure;

            var task = GetChildren()[0];

            // The child's only other entry point is OnEnter, which returns early while recharging -- and the
            // container above exits and re-enters this decorator every frame, so every entry after the first
            // one lands mid-recharge and is refused. By the time the cooldown expires nothing is left that
            // would enter the child, and OnUpdateInternal runs OnUpdate without entering. The child would
            // then execute carrying whatever state the previous pass left: a Wait would never reset its
            // timer, an animation node would never re-trigger.
            if (!task.IsRunning) task.OnNodeEnter();

            var result = task.OnUpdateInternal();
            if (result == ExecutionStatus.Running) return ExecutionStatus.Running;

            task.OnNodeExit();
            cooldownEndTime = Time.time + Duration.GetValue<float>();

            return result;
        }

        private bool IsCoolingDown => Time.time < cooldownEndTime;
    }
}

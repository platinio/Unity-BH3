using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Rolls once when the branch is entered so a behavior only fires part of the time. Useful for giving
    /// several agents running the same tree an unpredictable choice of attack.
    /// </summary>
    [GraphCreateMenu("Decorator/Random Chance")]
    public class RandomChance : Decorator
    {
        [DoNotSerialize]
        public ValueInput Chance { get; private set; }

        public override string NodeName => "Random Chance";

        public override string Description =>
            "Rolls once on enter. Runs the child when the roll passes, otherwise returns FAILURE without ticking it.";

        public override int MaxChildrenLimit => 1;
        
        public override bool ShowIcon => false;

        private bool rollPassed = false;

        protected override void Definition()
        {
            base.Definition();

            Chance = ValueInput<float>(nameof(Chance), 0.5f);
        }

        /// <summary>
        /// Rolls once per entry, and only records the answer — a failed roll never reaches the child below,
        /// so the child is never entered either. Entry itself is the tick's job; see
        /// <see cref="ContainerNode.TickChild"/>.
        /// </summary>
        public override void OnEnter()
        {
            rollPassed = Roll();
        }

        public override ExecutionStatus OnUpdate()
        {
            if (!rollPassed) return ExecutionStatus.Failure;
            if (GetChildren().Count == 0) return ExecutionStatus.Success;

            var task = GetChildren()[0];
            var result = TickChild(task);
            if (result == ExecutionStatus.Running) return ExecutionStatus.Running;

            task.OnNodeExit();

            return result;
        }

        /// <summary>
        /// Random.value is inclusive at both ends, so the bounds are handled explicitly to keep 0 meaning
        /// never and 1 meaning always.
        /// </summary>
        private bool Roll()
        {
            float chance = Chance.GetValue<float>();
            if (chance <= 0.0f) return false;
            if (chance >= 1.0f) return true;

            return Random.value < chance;
        }
    }
}

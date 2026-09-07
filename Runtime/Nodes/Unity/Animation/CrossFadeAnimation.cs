using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Animation/Cross Fade Animation")]
    public class CrossFadeAnimation : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Animator { get; private set; }
        [DoNotSerialize]
        public ValueInput StateName { get; private set; }
        [DoNotSerialize]
        public ValueInput Layer { get; private set; }
        [DoNotSerialize]
        public ValueInput TransitionDuration { get; private set; }
        [DoNotSerialize]
        public ValueInput TimeOffset { get; private set; }
        [DoNotSerialize]
        public ValueInput Duration { get; private set; }

        private float remainingDuration = 0.0f;
        private Animator targetAnimator;
        
        public override string NodeName => "Cross Fade Animation";
        public override string Description => "Cross Fade Animation to StateName";

        protected override void Definition()
        {
            base.Definition();

            Animator = ValueInput<object>(nameof(Animator), null);
            StateName = ValueInput<string>(nameof(StateName), string.Empty);
            Layer = ValueInput<int>(nameof(Layer), 0);
            TransitionDuration = ValueInput<float>(nameof(TransitionDuration), 0.0f);
            TimeOffset = ValueInput<float>(nameof(TimeOffset), 0.0f);
            Duration = ValueInput<float>(nameof(Duration), 0.0f);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            remainingDuration = Duration.GetValue<float>();

            if (!TryResolve(Animator, out targetAnimator)) return;

            string stateName = StateName.GetValue<string>();
            float normalizeTransitionDuration = TransitionDuration.GetValue<float>();
            int layer = Layer.GetValue<int>();
            float normalizeTimeOffset = TimeOffset.GetValue<float>();

            targetAnimator.CrossFade(stateName, normalizeTransitionDuration, layer, normalizeTimeOffset);
        }

        public override ExecutionStatus OnUpdate()
        {
            // Before the countdown, not after it: no cross-fade was started, so waiting out its duration
            // and then reporting Success would hold the branch for the full time and lie at the end of it.
            if (targetAnimator == null) return ExecutionStatus.Failure;

            remainingDuration -= Time.deltaTime;
            return remainingDuration <= 0? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Animation/Cross Fade Animation")]
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
        
        public override string NodeName => "Cross Fade Animation";

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
            remainingDuration = (float)Duration.GetValue();

            var animator = FindComponent<Animator>(Animator);
            string stateName = (string) StateName.GetValue();
            float normalizeTransitionDuration = (float) TransitionDuration.GetValue();
            int layer = (int) Layer.GetValue();
            float normalizeTimeOffset = (float) TimeOffset.GetValue();
            
            animator.CrossFade(stateName, normalizeTransitionDuration, layer, normalizeTimeOffset);
        }

        public override ExecutionStatus OnUpdate()
        {
            remainingDuration -= Time.deltaTime;
            return remainingDuration <= 0? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}
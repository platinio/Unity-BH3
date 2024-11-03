using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Animator/Play Animation")]
    public class PlayAnimation : GameplayNode
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
        
        public override string NodeName => "Play Animation";

        protected override void Definition()
        {
            base.Definition();

            Animator = ValueInput<string>(nameof(Animator), null);
            StateName = ValueInput<string>(nameof(StateName), string.Empty);
            Layer = ValueInput<int>(nameof(Layer), 0);
            TransitionDuration = ValueInput<string>(nameof(TransitionDuration), 0.0f);
            TimeOffset = ValueInput<string>(nameof(TimeOffset), 0.0f);
        }

        public override ExecutionStatus OnUpdate()
        {
            var animator = Animator.GetValue() as Animator;
            string stateName = (string) StateName.GetValue();
            float normalizeTransitionDuration = (float) TransitionDuration.GetValue();
            int layer = (int) Layer.GetValue();
            float normalizeTimeOffset = (float) TimeOffset.GetValue();
            
            animator.CrossFade(stateName, normalizeTransitionDuration, layer, normalizeTimeOffset);
            return ExecutionStatus.Success;
        }
    }
}
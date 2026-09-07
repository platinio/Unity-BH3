using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Animation/Set Animator Trigger")]
    public class SetAnimatorTrigger : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Animator { get; private set; }
        [DoNotSerialize]
        public ValueInput TriggerName { get; private set; }
       
        private Animator targetAnimator;

        public override string NodeName => "Set Animator Trigger";
        public override string Description => "Sets triggerName in the animator";

        protected override void Definition()
        {
            base.Definition();
           
            Animator = ValueInput<object>(nameof(Animator), null);
            TriggerName = ValueInput<string>(nameof(TriggerName), null);
        }

        public override void OnEnter()
        {
            if (!TryResolve(Animator, out targetAnimator)) return;

            string triggerName = TriggerName.GetValueOrDefault<string>();

            targetAnimator.SetTrigger(triggerName);
        }

        public override ExecutionStatus OnUpdate()
        {
            return targetAnimator == null ? ExecutionStatus.Failure : ExecutionStatus.Success;
        }
    }
}
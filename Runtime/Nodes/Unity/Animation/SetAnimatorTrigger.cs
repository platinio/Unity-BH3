using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Animation/Set Animator Trigger")]
    public class SetAnimatorTrigger : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Animator { get; private set; }
        [DoNotSerialize]
        public ValueInput TriggerName { get; private set; }
       
        public override string NodeName => "Set Animator Trigger";
        
        protected override void Definition()
        {
            base.Definition();
           
            Animator = ValueInput<object>(nameof(Animator), null);
            TriggerName = ValueInput<string>(nameof(TriggerName), null);
        }

        public override void OnEnter()
        {
            var animator = Animator.GetComponent<Animator>();
            string triggerName = TriggerName.GetValue() as string;
            
            animator.SetTrigger(triggerName);
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }
    }
}
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Animation/Set Animator Value")]
    public class SetAnimatorValue : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Animator { get; private set; }
        [DoNotSerialize]
        public ValueInput ValueName { get; private set; }
        [DoNotSerialize]
        public ValueInput Value { get; private set; }
        
        public override string NodeName => "Set Animator Value";
        public override string Description => "Sets value in the animator, value can be int/float/boolean";

        protected override void Definition()
        {
            base.Definition();
           
            Animator = ValueInput<object>(nameof(Animator), null);
            ValueName = ValueInput<string>(nameof(ValueName), null);
            Value = ValueInput<object>(nameof(Value), null);
        }

        public override void OnEnter()
        {
            base.OnEnter();

            var animator = GetComponent<Animator>(Animator);
            var valueName = ValueName.GetValue() as string;
            var animatorValue = Value.GetValue();
            
            if (animatorValue is int intValue) animator.SetInteger(valueName, intValue);
            if (animatorValue is float floatValue) animator.SetFloat(valueName, floatValue);
            if (animatorValue is bool boolValue) animator.SetBool(valueName, boolValue);
            
            Debug.LogError("Cant convert animator value to int, float or bool");
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }
    }
}
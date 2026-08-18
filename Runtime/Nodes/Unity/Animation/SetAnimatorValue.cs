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
            
            // else if, and the error in a final else. A boxed value is exactly one type, so these were never
            // alternatives that could both run -- but as three independent ifs with no return, the error ran
            // unconditionally, including after a set that worked. Every entry of this node logged a failure it
            // had not had, and Debug.LogError is expensive enough that across many agents it cost real time.
            if (animatorValue is int intValue) animator.SetInteger(valueName, intValue);
            else if (animatorValue is float floatValue) animator.SetFloat(valueName, floatValue);
            else if (animatorValue is bool boolValue) animator.SetBool(valueName, boolValue);
            else
            {
                Debug.LogError(
                    $"'{NodeName}' cannot set '{valueName}': an animator parameter is int, float or bool, and "
                    + $"Value is {(animatorValue == null ? "null" : animatorValue.GetType().Name)}.", gameObject);
            }
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }
    }
}
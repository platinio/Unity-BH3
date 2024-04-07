using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Animator/Set Animator Bool Value")]
    public class SetAnimatorBoolValue : GameplayNode
    {
        [Serialize] [Inspectable] private string varName = "";
        [Serialize] [Inspectable] private bool value = false;

        public override string NodeName => $"Set {varName} to {value}";

        public override ExecutionStatus OnUpdate()
        {
            if (!VariableDeclarations.IsDefined("Animator")) return ExecutionStatus.Failure;
            
            var animator = VariableDeclarations.Get<Animator>("Animator");
            animator.SetBool(varName, value);

            return ExecutionStatus.Success;
        }
    }

}


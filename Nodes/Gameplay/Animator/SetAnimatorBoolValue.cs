using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Animator/Set Animator Bool Value")]
    public class SetAnimatorBoolValue : GameplayNode
    {
        [Serialize] [Inspectable] private string m_varName = "";
        [Serialize] [Inspectable] private bool m_value = false;

        public override string NodeName => $"Set {m_varName} to {m_value}";

        public override ExecutionStatus OnUpdate()
        {
            if (!VariableDeclarations.IsDefined("Animator")) return ExecutionStatus.Failure;
            
            var animator = VariableDeclarations.Get<Animator>("Animator");
            animator.SetBool(m_varName, m_value);

            return ExecutionStatus.Success;
        }
    }

}


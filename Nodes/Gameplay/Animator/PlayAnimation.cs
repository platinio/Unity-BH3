using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Animator/Play Animation")]
    public class PlayAnimation : GameplayNode
    {
        [Serialize] [Inspectable] private string m_animationName = "";

        public override string NodeName => "Play Animation";

        public override ExecutionStatus OnUpdate()
        {
            if (!VariableDeclarations.IsDefined("Animator")) return ExecutionStatus.Failure;
            
            var animator = VariableDeclarations.Get<Animator>("Animator");
            animator.Play(m_animationName);

            return ExecutionStatus.Success;
        }
    }
}
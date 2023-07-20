using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Animator/Play Animation")]
    public class PlayAnimation : GameplayNode
    {
        [Serialize] [Inspectable] private string m_stateName = "";
        [Serialize] [Inspectable] private int m_layer = 0;
        [Serialize] [Inspectable] private float m_normalizeTransitionDiration = 0.15f;
        [Serialize] [Inspectable] private float m_normalizeTimeOffset = 0.0f;

        public override string NodeName => "Play Animation";

       
        public override ExecutionStatus OnUpdate()
        {
            if (!VariableDeclarations.IsDefined("Animator")) return ExecutionStatus.Failure;
            
            var animator = VariableDeclarations.Get<Animator>("Animator");

            animator.CrossFade(m_stateName, m_normalizeTransitionDiration, m_layer, m_normalizeTimeOffset);
            return ExecutionStatus.Success;
        }
    }
}
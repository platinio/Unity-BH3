using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Animator/Play Animation")]
    public class PlayAnimation : GameplayNode
    {
        [Serialize] [Inspectable] private string stateName = "";
        [Serialize] [Inspectable] private int layer = 0;
        [Serialize] [Inspectable] private float normalizeTransitionDiration = 0.15f;
        [Serialize] [Inspectable] private float normalizeTimeOffset = 0.0f;

        public override string NodeName => "Play Animation";

       
        public override ExecutionStatus OnUpdate()
        {
            if (!VariableDeclarations.IsDefined("Animator")) return ExecutionStatus.Failure;
            
            var animator = VariableDeclarations.Get<Animator>("Animator");

            animator.CrossFade(stateName, normalizeTransitionDiration, layer, normalizeTimeOffset);
            return ExecutionStatus.Success;
        }
    }
}
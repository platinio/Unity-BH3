using System.Collections.Generic;
using Platinio.AIPerception;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Face Target")]
    public class FaceTarget : GameplayNode
    {
        [Serialize] [Inspectable] private Vector3 rotatiionOffset;
        [Serialize] [Inspectable] private float rotationSpeed;
        [Serialize] [Inspectable] private float acceptableRotation;

        public override string NodeName => "Face Target";

        public override ExecutionStatus OnUpdate()
        {
            FaceAttackTarget();
            
            if (IsFacingTarget()) return ExecutionStatus.Success;
            return ExecutionStatus.Running;
        }

        private bool IsFacingTarget()
        {
            if (!VariableDeclarations.IsDefined("SelectedTargetEntities")) return false;

            var selectedTarget = VariableDeclarations.Get<List<GameEntity>>("SelectedTargetEntities")[0];
            if (selectedTarget == null) return false;
            
            
            Vector3 targetPosition = selectedTarget.transform.position;
            targetPosition.y = transform.position.y;
            Vector3 dir = (targetPosition - transform.position).normalized;
            Vector2 dir2D = new Vector2(dir.x, dir.z).normalized;
            Vector2 thisDir = new Vector2(transform.forward.x, transform.forward.z).normalized;

            return Vector2.Dot(dir2D, thisDir) > acceptableRotation;
        }

        private void FaceAttackTarget()
        {
            if (!VariableDeclarations.IsDefined("SelectedTargetEntities")) return;

            var selectedTarget = VariableDeclarations.Get<List<GameEntity>>("SelectedTargetEntities")[0];
            if (selectedTarget == null) return;
            
            Vector3 targetPosition = selectedTarget.transform.position;
            targetPosition.y = transform.position.y;
            Vector3 dir = (targetPosition - transform.position).normalized;
            Quaternion desireRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desireRot * Quaternion.Euler(rotatiionOffset), rotationSpeed * Time.deltaTime);
        }
    }

}


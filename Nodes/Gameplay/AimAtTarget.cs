using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Aim At Target")]
    public class AimAtTarget : GameplayNode
    {
        [Serialize] [Inspectable] private string m_entityKey;
        [Serialize] [Inspectable] private float m_desireAngle = 5.0f;
        [Serialize] [Inspectable] private float m_angularSpeed = 10.0f;

        public override ExecutionStatus OnUpdate()
        {
            var entity = VariableDeclarations.Get<IAIEntity>(m_entityKey);
            Vector3 targetAimPosition = new Vector3(entity.transform.position.x, 0, entity.transform.position.z);
            Vector3 currentAimPosition = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 aimDirection = (targetAimPosition - currentAimPosition).normalized;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aimDirection), m_angularSpeed);

            float angle = Vector3.Angle(aimDirection, transform.forward);
            return angle < m_desireAngle? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}


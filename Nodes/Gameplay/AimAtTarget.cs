using Platinio.GraphCore;
using Platinio.SkillGraph;
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
        [Serialize] [Inspectable] private float m_aimWeigthSpeed = 2.0f;

        private float m_aimWeight = 0.0f;
        private RangeWeapon m_rangeWeapon = null;

        public override void OnEnter()
        {
            m_aimWeight = 0;
            
            var entity = gameObject.GetComponent<IAIEntity>();
            entity.CharacterEquipment.TryGetEquipment(EquipmentType.RangeWeapon, out var item);

            m_rangeWeapon = item as RangeWeapon;
        }

        public override ExecutionStatus OnUpdate()
        {
            //RotateTowardsTarget();
            
            m_aimWeight += Time.deltaTime * m_aimWeigthSpeed;
            var entity = VariableDeclarations.Get<IAIEntity>(m_entityKey);
            
            //m_rangeWeapon.Aim(entity.AimPosition, m_aimWeight);
            return m_aimWeight > 1.0f? ExecutionStatus.Success : ExecutionStatus.Running;
        }

        private void RotateTowardsTarget()
        {
            var entity = VariableDeclarations.Get<IAIEntity>(m_entityKey);
            Vector3 targetAimPosition = new Vector3(entity.transform.position.x, 0, entity.transform.position.z);
            Vector3 currentAimPosition = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 aimDirection = (targetAimPosition - currentAimPosition).normalized;

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aimDirection), m_angularSpeed);
        }
    }
}


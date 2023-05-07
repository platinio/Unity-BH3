using Platinio.GraphCore;
using Platinio.SkillGraph;
using Unity.VisualScripting;


namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Start Aiming")]
    public class StartAiming : GameplayNode
    {
        [Serialize] [Inspectable] private string m_entityKey;

        public override string NodeName => "Start Aiming";

        public override ExecutionStatus OnUpdate()
        {
            var targetEntity = VariableDeclarations.Get<IAIEntity>(m_entityKey);
            var entity = gameObject.GetComponent<IAIEntity>();
            entity.CharacterEquipment.TryGetEquipment(EquipmentType.RangeWeapon, out var item);

            var rangeWeapon = item as RangeWeapon;
            rangeWeapon.StartAiming(targetEntity.AimTarget);
            
            return base.OnUpdate();
        }
    }
}


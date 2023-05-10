using Platinio.GraphCore;
using Platinio.SkillGraph;
using Unity.VisualScripting;


namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Stop Aiming")]
    public class StopAiming : GameplayNode
    {
        [Serialize] [Inspectable] private string m_entityKey;

        public override string NodeName => "Stop Aiming";

        public override ExecutionStatus OnUpdate()
        {
            var entity = gameObject.GetComponent<IAIEntity>();
            entity.CharacterEquipment.TryGetEquipment(EquipmentType.RangeWeapon, out var item);

            var rangeWeapon = item as RangeWeapon;
            if (rangeWeapon == null) return ExecutionStatus.Success;
            
            rangeWeapon.StopAiming();

            return ExecutionStatus.Success;
        }
    }
}
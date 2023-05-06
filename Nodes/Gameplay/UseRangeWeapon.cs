using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Skills/Use Range Weapon")]
    public class UseRangeWeapon : GameplayNode
    {
        public override ExecutionStatus OnUpdate()
        {
            var entity = gameObject.GetComponent<IAIEntity>();
            entity.CharacterEquipment.TryGetEquipment(EquipmentType.RangeWeapon, out var rangeWeapon);
            rangeWeapon.Use();

            return ExecutionStatus.Success;
        }
    }
}


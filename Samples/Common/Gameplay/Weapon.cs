using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;

        private AgentVariableWriter facts;

        public WeaponDefinition Definition => definition;

        private AgentVariableWriter Facts => facts != null ? facts : facts = AgentVariableWriter.On(gameObject);

        private void Awake()
        {
            if (definition != null) Publish();
        }

        public void Equip(WeaponDefinition newDefinition)
        {
            definition = newDefinition;
            Publish();
        }

        public void Attack(GameObject target)
        {
            if (definition.IsRanged) Shoot(target);
            else definition.Land(target, transform.position);
        }

        private void Shoot(GameObject target)
        {
            Vector3 muzzle = transform.position + Vector3.up;
            Projectile bolt = Instantiate(definition.Projectile, muzzle, Quaternion.identity);
            bolt.Launch(target, definition);
        }

        private void Publish()
        {
            Facts.Write(this, CombatFacts.WeaponId, definition.Id);
            Facts.Write(this, CombatFacts.AttackRange, definition.Range);
            Facts.Write(this, CombatFacts.AttackCooldown, definition.Cooldown);
        }
    }
}

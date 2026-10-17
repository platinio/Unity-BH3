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

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            Publish();
        }

        public void Equip(WeaponDefinition newDefinition)
        {
            definition = newDefinition;
            Publish();
        }

        public void Attack(GameObject target)
        {
            if (definition.IsRanged) Shoot(target);
            else Hit(target);
        }

        private void Hit(GameObject target)
        {
            if (target.TryGetComponent(out Health targetHealth)) targetHealth.Damage(definition.Damage);
        }

        private void Shoot(GameObject target)
        {
            Vector3 muzzle = transform.position + Vector3.up;
            Projectile bolt = Instantiate(definition.Projectile, muzzle, Quaternion.identity);
            bolt.Launch(target, definition.Damage);
        }

        private void Publish()
        {
            facts.Write(this, CombatFacts.WeaponId, definition.Id);
            facts.Write(this, CombatFacts.AttackRange, definition.Range);
            facts.Write(this, CombatFacts.AttackCooldown, definition.Cooldown);
        }
    }
}

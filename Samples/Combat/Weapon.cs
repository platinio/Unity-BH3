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

        private void Awake() => Publish();

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
            if (definition.Knockback > 0f && target.TryGetComponent(out Knockback knockback)) knockback.Push(PushDirectionTo(target) * definition.Knockback);
        }

        private Vector3 PushDirectionTo(GameObject target)
        {
            Vector3 away = target.transform.position - transform.position;
            away.y = 0f;
            return away.normalized;
        }

        private void Shoot(GameObject target)
        {
            Vector3 muzzle = transform.position + Vector3.up;
            Projectile bolt = Instantiate(definition.Projectile, muzzle, Quaternion.identity);
            bolt.Launch(target, definition.Damage);
        }

        private void Publish()
        {
            Facts.Write(this, CombatFacts.WeaponId, definition.Id);
            Facts.Write(this, CombatFacts.AttackRange, definition.Range);
            Facts.Write(this, CombatFacts.AttackCooldown, definition.Cooldown);
        }
    }
}

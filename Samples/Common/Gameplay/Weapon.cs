using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition definition;

        private AgentVariableWriter facts;
        private GameObject heldVisual;

        public WeaponDefinition Definition => definition;

        private AgentVariableWriter Facts => facts != null ? facts : facts = AgentVariableWriter.On(gameObject);

        private void Awake()
        {
            if (definition != null) Equip(definition);
        }

        public void Equip(WeaponDefinition newDefinition)
        {
            definition = newDefinition;
            Publish();
            ShowInHand();
        }

        public void Attack(GameObject target)
        {
            if (definition.IsRanged) Shoot(target);
            else definition.Land(target, gameObject);
        }

        private void Shoot(GameObject target)
        {
            Vector3 muzzle = transform.position + Vector3.up;
            Projectile bolt = Instantiate(definition.Projectile, muzzle, Quaternion.identity);
            bolt.Launch(target, definition, gameObject);
        }

        private void ShowInHand()
        {
            if (heldVisual != null) Destroy(heldVisual);
            heldVisual = null;

            Transform hand = Unit.Of(gameObject).WeaponSocket;
            if (definition.Visual == null || hand == null) return;

            heldVisual = Instantiate(definition.Visual, hand);
            heldVisual.transform.localPosition = Vector3.zero;
            heldVisual.transform.localRotation = Quaternion.identity;
        }

        private void Publish()
        {
            Facts.Write(this, CombatFacts.WeaponId, definition.Id);
            Facts.Write(this, CombatFacts.AttackRange, definition.Range);
            Facts.Write(this, CombatFacts.AttackCooldown, definition.Cooldown);
        }
    }
}

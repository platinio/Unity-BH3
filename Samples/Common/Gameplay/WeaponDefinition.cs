using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [CreateAssetMenu(menuName = "BH3 Samples/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        private const float SlowedSpeedFactor = 0.5f;

        [SerializeField] private BehaviorTreeGraphAsset attack;
        [SerializeField] private float range = 1.8f;
        [SerializeField] private float cooldown = 1f;
        [SerializeField] private int damage = 25;
        [SerializeField] private float knockback;
        [SerializeField] private float slowSeconds;
        [SerializeField] private Projectile projectile;
        [SerializeField] private GameObject visual;

        public BehaviorTreeGraphAsset Attack => attack;
        public float Range => range;
        public float Cooldown => cooldown;
        public int Damage => damage;
        public bool IsRanged => projectile != null;
        public Projectile Projectile => projectile;
        public GameObject Visual => visual;

        public void Land(GameObject target, GameObject attacker)
        {
            if (target.TryGetComponent(out Health health)) health.Damage(damage, attacker);
            if (knockback > 0f && target.TryGetComponent(out Knockback shove)) shove.Push(AwayFrom(attacker, target) * knockback);
            if (slowSeconds > 0f && target.TryGetComponent(out Slowed slowed)) slowed.Apply(slowSeconds, SlowedSpeedFactor);
        }

        private static Vector3 AwayFrom(GameObject attacker, GameObject target)
        {
            Vector3 away = target.transform.position - attacker.transform.position;
            away.y = 0f;
            return away.normalized;
        }
    }
}

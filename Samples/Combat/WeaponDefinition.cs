using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [CreateAssetMenu(menuName = "BH3 Samples/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] private float range = 1.8f;
        [SerializeField] private float cooldown = 1f;
        [SerializeField] private int damage = 25;
        [SerializeField] private Projectile projectile;

        public string Id => name;
        public float Range => range;
        public float Cooldown => cooldown;
        public int Damage => damage;
        public bool IsRanged => projectile != null;
        public Projectile Projectile => projectile;
    }
}

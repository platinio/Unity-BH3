using Platinio;
using UnityEngine;

namespace RPGDamage
{
    public class Hitbox : Damageable
    {
        [SerializeField] private float damageMultiplier = 1.0f;

        private DamageableManager damageableManager = null;
        private GameEntity gameEntity;
        private Collider hitCollider;

        public override GameEntity OwnerEntity => gameEntity;
        public DamageableManager DamageableManager => damageableManager;
        public float DamageMultiplier => damageMultiplier;
        public Collider HitCollider => hitCollider;

        private void Awake()
        {
            hitCollider = GetComponent<Collider>();
        }

        public void SetOwner(DamageableManager manager)
        {
            gameEntity = manager.GameEntity;
            damageableManager = manager;
        }

        public override void DoDamage(DamageInfo info)
        {
            info.Scale(DamageMultiplier);
            damageableManager.DoDamage( info, this );
        }
    }
}


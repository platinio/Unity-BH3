using ArcaneOnyx.Share;
using UnityEngine;
using UnityEngine.Events;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    public class DamageableManager : MonoBehaviour
    {
        [SerializeField] private UnityEvent OnKill;
        [SerializeField] private float maxHP;

        private float currentHP;
        
        public GameEntity GameEntity => ownerEntity;
        public float HP => currentHP;
        public bool Invulnerable { get; set; }
        public bool IsDead => isDead;
        public float MaxHP => maxHP;
      
        private GameEntity ownerEntity = null;
        
        protected bool isDead = false;

        protected virtual void Awake()
        {
            ownerEntity = GetComponentInParent<GameEntity>();
            SetDamageablePartsOwner();
        }

        private void SetDamageablePartsOwner()
        {
            //get all damageable
            Hitbox[] damageableArray = GetComponentsInChildren<Hitbox>();

            //set his owner
            for (int n = 0; n < damageableArray.Length; n++)
            {              
                damageableArray[n].SetOwner( this );
            }
        }
       
        public void DoDamage(DamageInfo damageInfo)
        {
            DoDamage(damageInfo, null);
        }

        public virtual void DoDamage(DamageInfo info, Hitbox hitbox)
        {
            //if we are dead just apply the impact force
            if (isDead)
            {
                return;
            }

            ModifyHP(-info.Damage);

            //modify HP can kill the instance too so check again for isDead
            if (HP <= 0.0f)
            {
                Kill(info.Attacker);
            }
        }

        private void Kill(GameEntity killer)
        {
            if (isDead) return;

            OnKill.Invoke();
            currentHP = 0;
            isDead = true;
        }

        public void KillImmediately()
        {
            ModifyHP(int.MinValue);
            Kill(null);
        }

        public void ModifyHP(float v)
        {
            currentHP += v;
        }
    }
}


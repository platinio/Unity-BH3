using System;
using RPGDamage;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    //basic class for all projectiles, arrow, bullets etc
    public class Projectile : MonoBehaviour
    {
        protected ShootInfo shootInfo;
        protected bool launched = false;
        protected Collider playerCollider = null;

        public ShootInfo ShootInfo { get { return shootInfo; } }

        protected virtual void Awake()
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag( "Player" );

            if(playerGO != null)
            {
                playerCollider = playerGO.GetComponent<Collider>();
            }
            
        }

        public virtual void Launch(ShootInfo info)
        {
            shootInfo = info;
            launched = true;
        }

        /// <summary>
        /// Do damage to a target
        /// </summary>
        /// <param name="target"></param>
        /// <returns>true if can do damage</returns>
        protected virtual bool TryDoDamage(Collider c)
        {
            Damageable[] damageable = c.GetComponents<Damageable>();

            if (damageable != null && damageable.Length > 0)
            {
                for (int n = 0; n < damageable.Length; n++)
                {
                    DamageInfo damageInfo = CreateDamageInfo(shootInfo);
                    damageable[n].DoDamage( damageInfo );

                    if (shootInfo.hitCallback != null)
                        shootInfo.hitCallback( damageable[n] );

                    
                }

                return true;
            }


            return false;
        }

        private DamageInfo CreateDamageInfo(ShootInfo info)
        {
            DamageInfo damageInfo = new DamageInfo(info.dmg, transform.forward, transform.position, info.sender);
            return damageInfo;
        }

        protected void ApplyImpactForce(Rigidbody rb , Vector3 impactPoint)
        {
            if (rb == null)
                return;

            rb.AddForceAtPosition( shootInfo.dir * shootInfo.hitForce, impactPoint );
        }
    }
}
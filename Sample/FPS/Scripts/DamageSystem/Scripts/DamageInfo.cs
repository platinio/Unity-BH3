using UnityEngine;

namespace RPGDamage
{
    /// <summary>
    /// abstraction for every single unique attack
    /// </summary>
    public class DamageInfo
    {
        public Vector3 HitPoint;
        public Vector3 Direction;
        public GameEntity Attacker;
        public GameEntity Target;
        public float Damage;
       
        public DamageInfo(float damage, Vector3 direction, Vector3 hitPoint, GameEntity attacker)
        {
            Damage = damage;
            Direction = direction;
            HitPoint = hitPoint;
            Attacker = attacker;
        }

        public void Scale(float scale)
        {
            Damage *= scale;
        }
    }
}
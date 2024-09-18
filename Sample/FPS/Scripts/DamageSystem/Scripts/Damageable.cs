using Platinio;
using Platinio.Share;
using UnityEngine;

namespace RPGDamage
{
    /// <summary>
    /// Basic class for all damageable stuff
    /// </summary>
    public abstract class Damageable : MonoBehaviour
    {
        public abstract GameEntity OwnerEntity { get; }
        public abstract void DoDamage(DamageInfo info);
    }
}


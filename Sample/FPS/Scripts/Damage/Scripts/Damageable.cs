using ArcaneOnyx.Share;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Sample
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


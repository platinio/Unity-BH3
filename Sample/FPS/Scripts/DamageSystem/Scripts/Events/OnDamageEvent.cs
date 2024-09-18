using UnityEngine.Events;

namespace ArcaneOnyx.BehaviorTree.Sample
{
    /// <summary>
    /// Event that gets called when a objects get some damage
    /// </summary>
    [System.Serializable]
    public class OnDamageEvent : UnityEvent<DamageInfo , Hitbox> { }

}



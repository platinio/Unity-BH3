using Platinio.Share;
using UnityEngine.Events;

namespace RPGDamage
{
    public interface IDamageableManager
    {
        float MaxHP { get; }
        float HP { get; }
        bool IsDead { get; }

        //void SetOwner(IGameEntity gameEntity);
    }
}


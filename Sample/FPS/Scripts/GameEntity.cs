using Platinio.FactionSystem;
using UnityEngine;

namespace ArcaneOnyx.Share
{
    public class GameEntity : MonoBehaviour
    {
        [SerializeField] private Faction faction;
        [SerializeField] private EntityNavAgent entityNavAgent;

        public Faction Faction => faction;
        public EntityNavAgent EntityNavAgent => entityNavAgent;
    }
}


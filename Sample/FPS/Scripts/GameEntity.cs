using ArcaneOnyx.Factions;
using ArcaneOnyx.ScriptableObjectDatabase;
using UnityEngine;

namespace ArcaneOnyx.Share
{
    public class GameEntity : MonoBehaviour
    {
        [SerializeField, ScriptableItemDatabaseSelector(typeof(FactionDatabase))] private Faction faction;
        [SerializeField] private EntityNavAgent entityNavAgent;

        public Faction Faction => faction;
        public EntityNavAgent EntityNavAgent => entityNavAgent;
    }
}


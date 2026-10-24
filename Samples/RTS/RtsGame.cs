using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    public sealed class RtsGame : MonoBehaviour
    {
        [SerializeField] private float checkInterval = 1f;

        private float nextCheckTime;

        public bool IsWon { get; private set; }

        private void Update()
        {
            if (IsWon || Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + checkInterval;

            IsWon = !AnyEnemyAlive();
        }

        private static bool AnyEnemyAlive()
        {
            foreach (Combatant combatant in Combatant.All)
            {
                if (combatant.Faction == Faction.Enemy && combatant.IsAlive) return true;
            }

            return false;
        }
    }
}

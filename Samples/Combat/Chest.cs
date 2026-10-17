using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class Chest : MonoBehaviour
    {
        private const float ReachDistance = 1.6f;
        private const float CheckInterval = 0.2f;

        [SerializeField] private WeaponDefinition weapon;

        private AgentVariableWriter facts;
        private Combatant playerInReach;
        private float nextCheckTime;

        public WeaponDefinition Weapon => weapon;

        private void Awake() => facts = AgentVariableWriter.On(gameObject);

        private void Update()
        {
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + CheckInterval;

            playerInReach = PlayerInReach();
            facts.Write(this, CombatFacts.PlayerNearby, playerInReach != null);
        }

        public bool GiveWeapon()
        {
            if (playerInReach == null) return false;

            playerInReach.GetComponent<Weapon>().Equip(weapon);
            return true;
        }

        private Combatant PlayerInReach()
        {
            foreach (Combatant combatant in Combatant.All)
            {
                if (combatant.Faction != Faction.Player || !combatant.IsAlive) continue;
                if (Vector3.Distance(transform.position, combatant.transform.position) <= ReachDistance) return combatant;
            }

            return null;
        }
    }
}

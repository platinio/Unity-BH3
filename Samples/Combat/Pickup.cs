using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.VisualScriptingExtension;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class Pickup : MonoBehaviour
    {
        private const float ReachDistance = 1.6f;
        private const float CheckInterval = 0.2f;
        private const string TakerInput = "taker";

        [SerializeField, PerCallInput(TakerInput, typeof(GameObject))]
        private FunctionCall<bool> effect;

        private AgentVariableWriter facts;
        private FunctionCall<bool>.Bound boundEffect;
        private InputSlot<GameObject> taker;
        private Combatant playerInReach;
        private float nextCheckTime;

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            boundEffect = effect.Bind(gameObject, name);
            taker = boundEffect.Input<GameObject>(TakerInput);
        }

        private void Update()
        {
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + CheckInterval;

            playerInReach = PlayerInReach();
            facts.Write(this, CombatFacts.PlayerNearby, playerInReach != null);
        }

        public bool Apply()
        {
            if (playerInReach == null) return false;

            boundEffect.Set(taker, playerInReach.gameObject);
            return boundEffect.Invoke();
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

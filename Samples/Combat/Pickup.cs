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
        private FunctionCall<bool> canPickup;

        [SerializeField, PerCallInput(TakerInput, typeof(GameObject))]
        private FunctionCall<bool> effect;

        private AgentVariableWriter facts;
        private FunctionCall<bool>.Bound boundCanPickup;
        private FunctionCall<bool>.Bound boundEffect;
        private InputSlot<GameObject> canPickupTaker;
        private InputSlot<GameObject> effectTaker;
        private Combatant playerInReach;
        private float nextCheckTime;

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            boundCanPickup = canPickup.Bind(gameObject, name + " canPickup");
            boundEffect = effect.Bind(gameObject, name + " effect");
            canPickupTaker = boundCanPickup.Input<GameObject>(TakerInput);
            effectTaker = boundEffect.Input<GameObject>(TakerInput);
        }

        private void Update()
        {
            if (Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + CheckInterval;

            playerInReach = PlayerInReach();
            facts.Write(this, CombatFacts.CanPickup, playerInReach != null && CanBeTakenBy(playerInReach));
        }

        public bool Apply()
        {
            if (playerInReach == null || !CanBeTakenBy(playerInReach)) return false;

            boundEffect.Set(effectTaker, playerInReach.gameObject);
            return boundEffect.Invoke();
        }

        private bool CanBeTakenBy(Combatant combatant)
        {
            boundCanPickup.Set(canPickupTaker, combatant.gameObject);
            return boundCanPickup.Invoke();
        }

        private Combatant PlayerInReach()
        {
            foreach (Combatant combatant in Combatant.All)
            {
                if (!combatant.IsPlayer || !combatant.IsAlive) continue;
                if (Vector3.Distance(transform.position, combatant.transform.position) <= ReachDistance) return combatant;
            }

            return null;
        }
    }
}

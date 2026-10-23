using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class StandsDownWhileMoving : MonoBehaviour
    {
        private AgentVariableWriter facts;
        private Sight sight;
        private Retaliation retaliation;
        private bool isStoodDown;

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            TryGetComponent(out sight);
            TryGetComponent(out retaliation);
        }

        private void Update()
        {
            if (IsWalkingAway) StandDown();
            else TakeUpArms();
        }

        // Attack-move is an unfinished move order too, so hold fire is the whole difference.
        private bool IsWalkingAway => HoldsFire && HasAnUnfinishedMoveOrder;

        private bool HoldsFire => facts.Read(RtsFacts.HoldsFire) is true;

        private bool HasAnUnfinishedMoveOrder
        {
            get
            {
                object ordered = facts.Read(ClickToMoveFacts.OrderNumber);

                return ordered != null && !ordered.Equals(facts.Read(ClickToMoveFacts.CompletedOrder));
            }
        }

        private void StandDown()
        {
            // Every tick, not once: either of the two below may have set a target earlier in the frame the
            // order arrived, before they were switched off.
            facts.Write(this, CombatFacts.HasTarget, false);

            if (isStoodDown) return;

            isStoodDown = true;
            IsWatchingForEnemies = false;
        }

        private void TakeUpArms()
        {
            if (!isStoodDown) return;

            isStoodDown = false;
            IsWatchingForEnemies = true;
        }

        private bool IsWatchingForEnemies
        {
            set
            {
                if (sight != null) sight.enabled = value;
                if (retaliation != null) retaliation.enabled = value;
            }
        }
    }
}

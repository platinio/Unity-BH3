using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class DefenseGame : MonoBehaviour
    {
        [SerializeField] private int startingPoints = 150;
        [SerializeField] private int startingLives = 10;

        private AgentVariableWriter facts;

        public int Points { get; private set; }
        public int Lives { get; private set; }
        public int Wave { get; private set; }

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            ResetBoard();
        }

        public bool TrySpend(int cost)
        {
            if (Points < cost) return false;

            Points -= cost;
            Publish();
            return true;
        }

        public void AddPoints(int amount)
        {
            Points += amount;
            Publish();
        }

        public void NextWave()
        {
            Wave++;
            Publish();
        }

        public void LoseLife()
        {
            Lives--;
            Publish();
            if (Lives <= 0) ResetBoard();
        }

        private void ResetBoard()
        {
            Points = startingPoints;
            Lives = startingLives;
            Wave = 1;
            Publish();
            foreach (Combatant combatant in Combatant.All.ToArray())
            {
                if (combatant.Faction == Faction.Enemy) Destroy(combatant.gameObject);
            }
        }

        private void Publish()
        {
            facts.Write(this, DefenseFacts.Points, Points);
            facts.Write(this, DefenseFacts.Lives, Lives);
            facts.Write(this, DefenseFacts.Wave, Wave);
        }
    }
}

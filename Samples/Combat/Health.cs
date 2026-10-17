using System;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;

        private int current;
        private AgentVariableWriter facts;

        public event Action Changed;

        public bool IsDead => current <= 0;
        public float Fraction => (float)current / maxHealth;

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            current = maxHealth;
            Publish();
        }

        public void Damage(int amount)
        {
            if (IsDead) return;

            current = Mathf.Max(0, current - amount);
            Publish();
        }

        public void Revive()
        {
            current = maxHealth;
            Publish();
        }

        private void Publish()
        {
            facts.Write(this, CombatFacts.IsDead, IsDead);
            facts.Write(this, CombatFacts.Health01, Fraction);
            Changed?.Invoke();
        }
    }
}

using System;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter), typeof(Unit))]
    public sealed class Health : MonoBehaviour
    {
        private const float HurtFlashSeconds = 0.15f;

        [SerializeField] private int maxHealth = 100;

        private int current;
        private AgentVariableWriter facts;
        private Unit unit;

        public event Action Changed;
        public event Action<GameObject> Damaged;

        public bool IsDead => current <= 0;
        public float Fraction => (float)current / maxHealth;

        private AgentVariableWriter Facts => facts != null ? facts : facts = AgentVariableWriter.On(gameObject);

        private void Awake()
        {
            current = maxHealth;
            unit = GetComponent<Unit>();
            Publish();
        }

        public void SetMaxHealth(int value)
        {
            maxHealth = value;
            current = value;
        }

        public void Damage(int amount, GameObject attacker)
        {
            if (IsDead) return;

            current = Mathf.Max(0, current - amount);
            FlashHurt();
            Publish();
            Damaged?.Invoke(attacker);
        }

        public void Heal(int amount)
        {
            if (IsDead) return;

            current = Mathf.Min(maxHealth, current + amount);
            Publish();
        }

        public void Revive()
        {
            current = maxHealth;
            Publish();
        }

        private void FlashHurt()
        {
            unit.RestoreRestingColors();
            unit.FlashColor(Color.red, HurtFlashSeconds);
        }

        private void Publish()
        {
            Facts.Write(this, CombatFacts.IsDead, IsDead);
            Facts.Write(this, CombatFacts.Health01, Fraction);
            Changed?.Invoke();
        }
    }
}

using System;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(AgentVariableWriter))]
    public sealed class Treasury : MonoBehaviour
    {
        [SerializeField] private int startingWood = 100;
        [SerializeField] private int startingGold = 150;

        private AgentVariableWriter facts;

        public event Action Changed;

        public int Wood { get; private set; }
        public int Gold { get; private set; }

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            Wood = startingWood;
            Gold = startingGold;
            Publish();
        }

        public bool CanAfford(int wood, int gold) => Wood >= wood && Gold >= gold;

        public bool TrySpend(int wood, int gold)
        {
            if (!CanAfford(wood, gold)) return false;

            Wood -= wood;
            Gold -= gold;
            Publish();
            return true;
        }

        public void Add(Resource kind, int amount)
        {
            if (kind == Resource.Wood) Wood += amount;
            else Gold += amount;
            Publish();
        }

        private void Publish()
        {
            facts.Write(this, RtsFacts.Wood, Wood);
            facts.Write(this, RtsFacts.Gold, Gold);
            Changed?.Invoke();
        }
    }
}

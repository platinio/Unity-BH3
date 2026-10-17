using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [RequireComponent(typeof(Building))]
    public sealed class ProductionQueue : MonoBehaviour
    {
        private readonly Queue<UnitKind> waiting = new();
        private AgentVariableWriter facts;

        public int Count => waiting.Count;
        public UnitKind Next => waiting.Count > 0 ? waiting.Peek() : null;

        private void Awake()
        {
            facts = AgentVariableWriter.On(gameObject);
            Publish();
        }

        public void Enqueue(UnitKind kind)
        {
            waiting.Enqueue(kind);
            Publish();
        }

        public UnitKind Dequeue()
        {
            UnitKind kind = waiting.Dequeue();
            Publish();
            return kind;
        }

        private void Publish()
        {
            facts.Write(this, RtsFacts.QueueCount, waiting.Count);
            facts.Write(this, RtsFacts.TrainSeconds, Next != null ? Next.TrainSeconds : 0f);
            facts.Write(this, RtsFacts.NextKind, Next != null ? Next.Id : "");
        }
    }
}

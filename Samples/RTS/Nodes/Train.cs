using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/RTS/Train")]
    public class Train : GameplayNode
    {
        public override string NodeName => "Train";
        public override string Description => "Works on the unit at the head of this building's production queue for its training time, publishing how far along it is, and succeeds when it is ready. Fails when the queue is empty.";

        private sealed class Memory
        {
            public float StartedAt;
            public float Seconds;
        }

        public override void OnEnter(BTContext ctx)
        {
            ProductionQueue queue = ctx.GetComponent<ProductionQueue>();
            Memory memory = ctx.Memory<Memory>();
            memory.StartedAt = Time.time;
            memory.Seconds = queue != null && queue.Next != null ? queue.Next.TrainSeconds : 0f;
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            ProductionQueue queue = ctx.GetComponent<ProductionQueue>();
            if (queue == null || queue.Next == null) return ExecutionStatus.Failure;

            Memory memory = ctx.Memory<Memory>();
            float elapsed = Time.time - memory.StartedAt;
            queue.SetProgress(memory.Seconds <= 0f ? 1f : elapsed / memory.Seconds);

            return queue.Progress01 >= 1f ? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}

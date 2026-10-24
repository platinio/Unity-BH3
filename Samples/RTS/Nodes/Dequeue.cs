using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/RTS/Dequeue")]
    public class Dequeue : GameplayNode
    {
        public override string NodeName => "Dequeue";
        public override string Description => "Removes the unit at the head of this building's production queue. Fails when the queue is empty.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            ProductionQueue queue = ctx.GetComponent<ProductionQueue>();
            if (queue == null || queue.Count == 0) return ExecutionStatus.Failure;

            queue.Dequeue();
            return ExecutionStatus.Success;
        }
    }
}

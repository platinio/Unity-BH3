using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.GraphCore;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BH3.Samples
{
    [GraphCreateMenu("Samples/RTS/Spawn Unit")]
    public class SpawnUnit : GameplayNode
    {
        private const float MaxDistanceToNavMesh = 3f;

        public override string NodeName => "Spawn Unit";
        public override string Description => "Spawns the unit at the head of this building's production queue at its rally point. Fails when the queue is empty.";

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            ProductionQueue queue = ctx.GetComponent<ProductionQueue>();
            Building building = ctx.GetComponent<Building>();
            if (queue == null || building == null || queue.Next == null) return ExecutionStatus.Failure;

            Spawn(queue.Next, RallySpot(building));
            return ExecutionStatus.Success;
        }

        private static Vector3 RallySpot(Building building) =>
            NavMesh.SamplePosition(building.RallyPoint, out NavMeshHit hit, MaxDistanceToNavMesh, NavMesh.AllAreas) ? hit.position : building.RallyPoint;

        private static void Spawn(UnitKind kind, Vector3 spot)
        {
            RtsUnit unit = Object.Instantiate(kind.Prefab, spot, Quaternion.identity);
            unit.name = kind.Id;
            unit.Apply(kind);
            unit.gameObject.SetActive(true);
        }
    }
}

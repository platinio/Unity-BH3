using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Generate Random Navmesh Position")]
    public class GenerateRandomNavMeshPosition : GameplayNode
    {
        [Header("Config")]
        [Serialize, Inspectable] private float MinDistance;
        [Serialize, Inspectable] private float MaxDistance;
        [Serialize, Inspectable] private int MaxTries = 10;
        [Serialize, Inspectable] private float SampleDistance = 5.0f;

        [Header("Keys")] [Serialize, Inspectable]
        private string GeneratePositionKey;

        public override string NodeName => "Generate Random Nav Mesh Position";

        public override ExecutionStatus OnUpdate()
        {
            for (int i = 0; i < MaxTries; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                float d = Random.Range(MinDistance, MaxDistance);

                Vector3 randomPosition = transform.position + (new Vector3(dir.x, 0.0f, dir.y) * d);
                
                if (NavMesh.SamplePosition(randomPosition, out var hit, SampleDistance, NavMesh.AllAreas))
                {
                    SavePosition(hit.position);
                    return ExecutionStatus.Success;
                }
            }

            return ExecutionStatus.Failure;
        }

        private void SavePosition(Vector3 position)
        {
            Machine.Variables.declarations.Set(GeneratePositionKey, position);
        }

    }

}


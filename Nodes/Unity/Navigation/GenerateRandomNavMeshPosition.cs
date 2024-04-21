using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Generate Random Navmesh Position")]
    public class GenerateRandomNavMeshPosition : GameplayNode
    {
        [Header("Config")]
        [Serialize, Inspectable] private FloatBlackboardVariable MinDistance = new(0.0f);
        [Serialize, Inspectable] private FloatBlackboardVariable MaxDistance = new(0.0f);
        [Serialize, Inspectable] private IntBlackboardVariable MaxTries = new(10);
        [Serialize, Inspectable] private FloatBlackboardVariable SampleDistance = new(5.0f);

        [Header("Keys")] [Serialize, Inspectable]
        private string GeneratePositionKey;

        public override string NodeName => "Generate Random Nav Position";

        public override ExecutionStatus OnUpdate()
        {
            for (int i = 0; i < MaxTries.GetValue(BehaviorTreeMachine); i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                float d = Random.Range(MinDistance.GetValue(BehaviorTreeMachine), MaxDistance.GetValue(BehaviorTreeMachine));

                Vector3 randomPosition = transform.position + (new Vector3(dir.x, 0.0f, dir.y) * d);
                
                if (NavMesh.SamplePosition(randomPosition, out var hit, SampleDistance.GetValue(BehaviorTreeMachine), NavMesh.AllAreas))
                {
                    SavePosition(hit.position);
                    return ExecutionStatus.Success;
                }
            }

            return ExecutionStatus.Failure;
        }

        private void SavePosition(Vector3 position)
        {
            BehaviorTreeMachine.GraphInstance.declarations.Set(GeneratePositionKey, position);
        }

    }

}


using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Generate Random Navmesh Position")]
    public class GenerateRandomNavMeshPosition : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput MinDistance { get; private set; }
        [DoNotSerialize]
        public ValueInput MaxDistance { get; private set; }
        [DoNotSerialize]
        public ValueInput MaxTries { get; private set; }
        [DoNotSerialize]
        public ValueInput SampleDistance { get; private set; }

        [Serialize, Inspectable] private VariableKind VariableKind;
        [DoNotSerialize]
        public ValueInput PositionKey { get; private set; }

        public override string NodeName => "Generate Random Nav Position";
        public override string Description => "Generates a random nav mesh position, returns SUCCESS/FAILURE indicating if it was possible to generate";

        protected override void Definition()
        {
            base.Definition();

            MinDistance = ValueInput<float>(nameof(MinDistance), 0.0f);
            MaxDistance = ValueInput<float>(nameof(MaxDistance), 0.0f);
            MaxTries = ValueInput<int>(nameof(MaxTries), 3);
            SampleDistance = ValueInput<float>(nameof(SampleDistance), 0.0f);
            PositionKey = ValueInput<string>(nameof(PositionKey), string.Empty);
        }

        public override ExecutionStatus OnUpdate()
        {
            for (int i = 0; i < MaxTries.GetValue<int>(); i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                float d = Random.Range(MinDistance.GetValue<float>(), MaxDistance.GetValue<float>());

                Vector3 randomPosition = transform.position + (new Vector3(dir.x, 0.0f, dir.y) * d);
                
                if (NavMesh.SamplePosition(randomPosition, out var hit, SampleDistance.GetValue<float>(), NavMesh.AllAreas))
                {
                    SavePosition(hit.position);
                    return ExecutionStatus.Success;
                }
            }

            return ExecutionStatus.Failure;
        }

        private void SavePosition(Vector3 value)
        {
            SaveVariable(PositionKey.GetValue<string>(), VariableKind, value);
        }
    }
}


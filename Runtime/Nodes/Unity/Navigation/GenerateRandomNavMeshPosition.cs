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

            // A sample radius of zero can never hit the navmesh, so the old default made this node return
            // Failure forever for anyone who left it alone. One metre is the smallest radius that actually
            // finds a surface under a point that is roughly on one.
            SampleDistance = ValueInput<float>(nameof(SampleDistance), 1.0f);
            PositionKey = ValueInput<string>(nameof(PositionKey), string.Empty);
        }

        public override ExecutionStatus OnUpdate()
        {
            // Read once rather than per iteration: the loop condition re-evaluated the port every pass, and a
            // port read can reach a variable lookup or a whole script graph.
            int maxTries = (int) MaxTries.GetValue();

            // (float), not (int). GetValue returns the raw boxed object and unboxing does not convert, so
            // casting a boxed float to int throws InvalidCastException -- which this node did on its first
            // tick with its own default, before anyone connected anything to it.
            float sampleDistance = (float) SampleDistance.GetValue();

            for (int i = 0; i < maxTries; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                float d = Random.Range((float) MinDistance.GetValue(), (float)MaxDistance.GetValue());

                Vector3 randomPosition = transform.position + (new Vector3(dir.x, 0.0f, dir.y) * d);

                if (NavMesh.SamplePosition(randomPosition, out var hit, sampleDistance, NavMesh.AllAreas))
                {
                    SavePosition(hit.position);
                    return ExecutionStatus.Success;
                }
            }

            return ExecutionStatus.Failure;
        }

        private void SavePosition(Vector3 value)
        {
            SaveVariable((string)PositionKey.GetValue(), VariableKind, value);
        }
    }
}


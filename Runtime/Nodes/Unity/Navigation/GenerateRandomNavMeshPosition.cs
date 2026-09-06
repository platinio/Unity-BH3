using System.Collections.Generic;
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

        [Serialize, Inspectable] private BehaviorTreeVariableKind VariableKind;
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

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);
            VariableKeyPort.CollectProblems(PositionKey, into);
            VariableKindField.CollectProblems(VariableKind, into);
        }

        public override ExecutionStatus OnUpdate()
        {
            // Before any sampling. A node with no key cannot succeed however many points it tries, so the
            // work is wasted and the complaint arrives late — and until this resolved through
            // VariableKeyPort at all, an empty key wrote a variable called "" and still reported Success.
            string positionKey = VariableKeyPort.Resolve(PositionKey, NodeName);

            // Read once rather than per iteration: the loop condition re-evaluated the port every pass, and a
            // port read can reach a variable lookup or a whole script graph.
            int maxTries = MaxTries.GetValue<int>();
            float sampleDistance = SampleDistance.GetValue<float>();

            for (int i = 0; i < maxTries; i++)
            {
                Vector2 dir = Random.insideUnitCircle;
                float d = Random.Range(MinDistance.GetValue<float>(), MaxDistance.GetValue<float>());

                Vector3 randomPosition = transform.position + (new Vector3(dir.x, 0.0f, dir.y) * d);

                if (NavMesh.SamplePosition(randomPosition, out var hit, sampleDistance, NavMesh.AllAreas))
                {
                    SaveVariable(positionKey, VariableKind, hit.position);
                    return ExecutionStatus.Success;
                }
            }

            return ExecutionStatus.Failure;
        }

    }
}


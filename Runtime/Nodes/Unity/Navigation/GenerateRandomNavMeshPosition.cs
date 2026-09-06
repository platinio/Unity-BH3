using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Offers a random point on the navmesh near the agent, plus whether it found one.
    ///
    /// <para>
    /// <b>A data node, not an action.</b> It never enters the execution flow
    /// (<see cref="CanBeUsedAsTransitionDestination"/> is false) and runs nothing on its own schedule; the
    /// sample happens when another node pulls one of the outputs. It used to be an action that wrote its
    /// answer into a variable and reported Success or Failure, which meant a caller wanting the position
    /// had to agree with it on a key, read that key back through a second node, and trust that nothing
    /// else wrote the same name in between. A value belongs on a port.
    /// </para>
    ///
    /// <para>
    /// Both outputs describe the <em>same</em> sample — one per frame, cached — for the reason
    /// <c>TacticalPositionSelection</c> caches: the pick is random, so a second run answers differently,
    /// and a validity flag from one run beside a position from another is a lie with two ports.
    /// </para>
    ///
    /// <para>
    /// Failing to find a point is an ordinary outcome, not a misconfiguration, so it is reported rather
    /// than thrown: <see cref="HasPosition"/> goes false and <see cref="Position"/> answers
    /// <see cref="Vector3.zero"/>. A branch that must not act without a point gates on
    /// <see cref="HasPosition"/> through a Boolean Condition — which is what the Failure this node used to
    /// return was doing, made explicit on the canvas.
    /// </para>
    /// </summary>
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

        [DoNotSerialize]
        public ValueOutput Position { get; private set; }

        [DoNotSerialize]
        public ValueOutput HasPosition { get; private set; }

        /// <summary>The sample both outputs describe. Valid only for <see cref="lastFrame"/>.</summary>
        [DoNotSerialize] private Vector3 lastPosition;

        [DoNotSerialize] private bool lastFound;

        [DoNotSerialize] private int lastFrame = -1;

        public override string NodeName => "Generate Random Nav Position";

        public override string Description =>
            "Offers a random navmesh position near the agent, and whether one was found";

        public override bool CanBeUsedAsTransitionDestination => false;

        // Four inputs and two outputs, all fixed, so the size chosen at creation never goes stale --
        // the same reason TacticalPositionSelection sets one.
        public override Vector2 StartingSize => new(240.0f, 180.0f);

        protected override void Definition()
        {
            base.Definition();

            MinDistance = ValueInput<float>(nameof(MinDistance), 0.0f);
            MaxDistance = ValueInput<float>(nameof(MaxDistance), 0.0f);
            MaxTries = ValueInput<int>(nameof(MaxTries), 3);

            // A sample radius of zero can never hit the navmesh, so the old default made this node find
            // nothing forever for anyone who left it alone. One metre is the smallest radius that actually
            // finds a surface under a point that is roughly on one.
            SampleDistance = ValueInput<float>(nameof(SampleDistance), 1.0f);

            Position = ValueOutput<Vector3>(nameof(Position), () =>
            {
                Sample();
                return lastPosition;
            });

            HasPosition = ValueOutput<bool>(nameof(HasPosition), () =>
            {
                Sample();
                return lastFound;
            });
        }

        /// <summary>
        /// The frame's sample, run on the first pull and reread by every later one. The cache is written
        /// only after a run completes, so a pull that throws leaves nothing stale behind.
        /// </summary>
        private void Sample()
        {
            if (lastFrame == Time.frameCount) return;

            // Read once rather than per iteration: the loop condition re-evaluated the port every pass, and
            // a port read can reach a variable lookup or a whole script graph.
            int maxTries = MaxTries.GetValue<int>();
            float sampleDistance = SampleDistance.GetValue<float>();
            float minDistance = MinDistance.GetValue<float>();
            float maxDistance = MaxDistance.GetValue<float>();

            var origin = transform.position;
            var found = false;
            var position = Vector3.zero;

            for (int i = 0; i < maxTries; i++)
            {
                Vector2 direction = Random.insideUnitCircle;
                float distance = Random.Range(minDistance, maxDistance);

                Vector3 candidate = origin + (new Vector3(direction.x, 0.0f, direction.y) * distance);

                if (!NavMesh.SamplePosition(candidate, out var hit, sampleDistance, NavMesh.AllAreas)) continue;

                position = hit.position;
                found = true;
                break;
            }

            lastPosition = position;
            lastFound = found;
            lastFrame = Time.frameCount;
        }
    }
}

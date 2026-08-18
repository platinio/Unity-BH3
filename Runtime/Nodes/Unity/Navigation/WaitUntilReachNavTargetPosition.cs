using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Wait Until Reach Nav Target Position")]
    public class WaitUntilReachNavTargetPosition : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        public override string NodeName => "Wait Until Reach Nav Target Position";

        private NavMeshAgent navAgent = null;

        protected override void Definition()
        {
            base.Definition();
            
            // Read through GetComponent, which never calls GetValue -- unconnected falls back to the agent.
            Target = ValueInput<GameObject>(nameof(Target)).SafeToLeaveUnconnected();
        }
        
        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>(Target);
        }

        public override ExecutionStatus OnUpdate()
        {
            // remainingDistance is not meaningful until the path is computed.
            if (navAgent.pathPending) return ExecutionStatus.Running;

            // The agent stops moving at its own stoppingDistance, so arrival must be judged against it —
            // a fixed 1mm threshold waits forever on any agent with a non-zero stopping distance. The
            // floor keeps the old behavior for agents that do drive all the way onto the point.
            float arriveDistance = Mathf.Max(navAgent.stoppingDistance, 0.001f);
            return navAgent.remainingDistance <= arriveDistance
                ? ExecutionStatus.Success
                : ExecutionStatus.Running;
        }
    }
}
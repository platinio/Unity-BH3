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
            float d = Vector3.Distance( navAgent.transform.position, navAgent.destination);
            return d < 0.001f ? ExecutionStatus.Success : ExecutionStatus.Running;
        }
    }
}
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Stop NavAgent")]
    public class StopNavAgent : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        public override string NodeName => "Stop NavAgent";

        private NavMeshAgent navAgent = null;
        
        public override void OnEnter()
        {
            base.OnEnter();

            if (!TryResolve(Target, out navAgent)) return;

            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
        
        protected override void Definition()
        {
            base.Definition();
            // Read through GetComponent, which never calls GetValue -- unconnected falls back to the agent.
            Target = ValueInput<GameObject>(nameof(Target)).SafeToLeaveUnconnected();
        }

        public override ExecutionStatus OnUpdate()
        {
            return navAgent == null ? ExecutionStatus.Failure : ExecutionStatus.Success;
        }
    }
}
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Navigation/Set Nav Agent Speed")]
    public class SetNavAgentSpeed : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }

        [DoNotSerialize]
        public ValueInput Speed { get; private set; }

        public override string NodeName => "Set Nav Agent Speed";
        public override string Description => "Makes the nav agent walk at Speed metres per second until something sets another speed";

        private NavMeshAgent navAgent = null;

        protected override void Definition()
        {
            base.Definition();

            // Read through GetComponent, which never calls GetValue -- unconnected falls back to the agent.
            Target = ValueInput<Object>(nameof(Target)).SafeToLeaveUnconnected();
            Speed = ValueInput<float>(nameof(Speed));
        }

        public override void OnEnter()
        {
            base.OnEnter();

            if (!TryResolve(Target, out navAgent)) return;

            navAgent.speed = Speed.GetValue<float>();
        }

        public override ExecutionStatus OnUpdate()
        {
            return navAgent == null ? ExecutionStatus.Failure : ExecutionStatus.Success;
        }
    }
}

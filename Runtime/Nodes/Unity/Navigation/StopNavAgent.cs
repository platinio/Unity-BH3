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
        
        public override void OnAwake()
        {
            navAgent = GetTarget(Target).GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
        }
        
        protected override void Definition()
        {
            base.Definition();
            Target = ValueInput<GameObject>(nameof(Target));
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }
    }
}
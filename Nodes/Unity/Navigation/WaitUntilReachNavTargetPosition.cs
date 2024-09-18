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
            
            Target = ValueInput<GameObject>(nameof(Target));
        }
        
        public override void OnAwake()
        {
            navAgent = GetTarget(Target).GetComponent<NavMeshAgent>();
        }

        public override ExecutionStatus OnUpdate() => navAgent.remainingDistance < Mathf.Epsilon ? ExecutionStatus.Success : ExecutionStatus.Running;
    }
}
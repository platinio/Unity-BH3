using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Wait Until Reach Nav Target Position")]
    public class WaitUntilReachNavTargetPosition : GameplayNode
    {
        [Serialize, Inspectable] private GameObjectBlackboardVariable Target = new();
        
        public override string NodeName => "Wait Until Reach Nav Target Position";

        private NavMeshAgent navAgent = null;

        public override void OnAwake()
        {
            navAgent = GetTargetGameObject(Target).GetComponent<NavMeshAgent>();
        }

        public override ExecutionStatus OnUpdate() => navAgent.remainingDistance < Mathf.Epsilon ? ExecutionStatus.Success : ExecutionStatus.Running;
    }
}
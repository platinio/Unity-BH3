using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Set NavAgent Position")]
    public class SetNavAgentPosition : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Target { get; private set; }
        
        [DoNotSerialize]
        public ValueInput NavPosition { get; private set; }
       
        [Serialize, Inspectable] protected bool ClearCurrentPath = false;
        [Serialize, Inspectable] protected bool WaitForPathComplete = false;

        public override string NodeName => "Set Nav Agent Position";
        public override string Description => "Updates the nav agent destination";

        protected NavMeshAgent navAgent = null;
        private bool setPositionWasCompleted = false;

        protected override void Definition()
        {
            base.Definition();
            
            // Read through GetComponent, which never calls GetValue -- unconnected falls back to the agent.
            Target = ValueInput<Object>(nameof(Target)).SafeToLeaveUnconnected();
            NavPosition = ValueInput<Vector3>(nameof(NavPosition));
        }
        
        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>(Target);
        }

        public override void OnEnter()
        {
            if (navAgent == null)
            {
                navAgent = GetComponent<NavMeshAgent>(Target);
            }
            
            if (!WaitForPathComplete)
            {
                navAgent.updatePosition = true;
                navAgent.updateRotation = true;
                navAgent.isStopped = false;
            }

            if (ClearCurrentPath) navAgent.ResetPath();

            setPositionWasCompleted = TryUpdateNavAgentPosition();
        }

        public override ExecutionStatus OnUpdate()
        {
            if (!WaitForPathComplete) return setPositionWasCompleted? ExecutionStatus.Success : ExecutionStatus.Failure;
            if (!setPositionWasCompleted) return ExecutionStatus.Failure;
            
            if (navAgent.pathPending) return ExecutionStatus.Running;
            if (navAgent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                if (WaitForPathComplete)
                {
                    navAgent.updatePosition = true;
                    navAgent.updateRotation = true;
                    navAgent.isStopped = false;
                }

                return ExecutionStatus.Success;
            }

            return ExecutionStatus.Failure;
        }

        public override void OnExit()
        {
           
        }

        protected virtual bool TryUpdateNavAgentPosition()
        {
            Vector3 pos = NavPosition.GetValue<Vector3>();
            return navAgent.SetDestination(pos);
        }
    }
}


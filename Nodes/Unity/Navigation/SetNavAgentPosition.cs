using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Set NavAgent Position")]
    public class SetNavAgentPosition : GameplayNode
    {
        [Serialize, Inspectable] protected GameObjectBlackboardVariable Target = new();
        [Serialize, Inspectable] protected Vector3BlackboardVariable NavPosition = new ();
        [Serialize, Inspectable] protected bool ClearCurrentPath = false;
        [Serialize, Inspectable] protected bool WaitForPathComplete = false;

        public override string NodeName => "Set Nav Agent Position";

        private NavMeshAgent navAgent = null;
        private bool setPositionWasCompleted = false;

        public override void OnAwake()
        {
            navAgent = GetTargetGameObject(Target).GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
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
            Vector3 pos = NavPosition.GetValue(BehaviorTreeMachine);
            return navAgent.SetDestination(pos);
        }
    }
}


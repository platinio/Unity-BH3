using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Set NavAgent Position")]
    public class SetNavAgentPosition : GameplayNode
    {
        [Serialize, Inspectable] protected string NavPositionVariableName;
        [Serialize, Inspectable] private bool ClearCurrentPath = false;
        [Serialize, Inspectable] private bool WaitForPathComplete = false;
       
        public override string NodeName => "Set Nav Agent Position";

        private NavMeshAgent navAgent = null;

        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>();
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
            TryUpdateNavAgentPosition();
        }

        public override ExecutionStatus OnUpdate()
        {
            if (!WaitForPathComplete) return ExecutionStatus.Success;
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

        protected virtual void TryUpdateNavAgentPosition()
        {
            if (!Machine.Variables.declarations.IsDefined(NavPositionVariableName)) return;
            
            Vector3 pos = Machine.Variables.declarations.Get<Vector3>(NavPositionVariableName);
            navAgent.SetDestination(pos);
        }
    }
}


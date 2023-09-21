using System.Collections.Generic;
using Platinio.AIPerception;
using Platinio.GraphCore;
using UnityEngine.AI;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Navigation/Follow Target")]
    public class FollowTarget : SetNavAgentPosition
    {
        public override string NodeName => "Follow Target";

        private NavMeshAgent navAgent = null;

        public override void OnAwake()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnEnter()
        {
            navAgent.updatePosition = true;
            navAgent.updateRotation = true;

            navAgent.isStopped = false;
            TryUpdateNavAgentPosition();
        }

        public override ExecutionStatus OnUpdate()
        {
            if (navAgent.pathPending) return ExecutionStatus.Running;
            if (navAgent.pathStatus == NavMeshPathStatus.PathComplete) return ExecutionStatus.Success;

            return ExecutionStatus.Failure;
        }

        private void TryUpdateNavAgentPosition()
        {
            if (!VariableDeclarations.IsDefined("SelectedTargetEntities")) return;

            var selectedTarget = VariableDeclarations.Get<List<GameEntity>>("SelectedTargetEntities")[0];
            if (selectedTarget == null) return;

            navAgent.SetDestination(selectedTarget.transform.position);
        }
    }
}
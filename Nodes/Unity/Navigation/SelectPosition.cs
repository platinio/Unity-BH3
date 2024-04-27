using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Unity/Navigation/Select Position")]
    public class SelectPosition : GameplayNode
    {
        [Serialize] [Inspectable] private FloatBlackboardVariable DistanceFromTarget = new();
        [Serialize] [Inspectable] private Vector3BlackboardVariable TargetPosition;
        [Serialize] [Inspectable] private string DesirePositionKey;

        public override string NodeName => "Select Position";

        public override void OnEnter()
        {
            Machine.Variables.declarations.Set(DesirePositionKey, CalculateDesirePosition());
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }

        private Vector3 CalculateDesirePosition()
        {
            Vector3 to = TargetPosition.GetValue(BehaviorTreeMachine);
            Vector3 from = transform.position;

            Vector3 dir = (to - from).normalized;
            return to - (dir * DistanceFromTarget.GetValue(BehaviorTreeMachine));
        }

    }
}


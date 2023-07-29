using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Navigation/Select Position")]
    public class SelectPosition : GameplayNode
    {
        [Serialize] [Inspectable] private float DistanceFromTarget;
        [Serialize] [Inspectable] private string TargetPositionKey;
        [Serialize] [Inspectable] private string DesirePositionKey;

        public override string NodeName => "Select Position";

        public override void OnEnter()
        {
            Machine.Variables.Set(DesirePositionKey, CalculateDesirePosition());
        }

        public override ExecutionStatus OnUpdate()
        {
            return ExecutionStatus.Success;
        }

        private Vector3 CalculateDesirePosition()
        {
            Vector3 to = GetPosition(TargetPositionKey);
            Vector3 from = transform.position;

            Vector3 dir = (to - from).normalized;
            return to - (dir * DistanceFromTarget);
        }

    }
}


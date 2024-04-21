using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Wait Range")]
    public class WaitTimeRandomRange : GameplayNode
    {
        [Serialize] [Inspectable] private FloatBlackboardVariable minTime = new();
        [Serialize] [Inspectable] private FloatBlackboardVariable maxTime = new();

        private float timer = 0.0f;

        public override string NodeName => $"Wait Range";

        public override void OnEnter()
        {
            timer = Random.Range(minTime.GetValue(BehaviorTreeMachine), maxTime.GetValue(BehaviorTreeMachine));
        }

        public override ExecutionStatus OnUpdate()
        {
            timer -= Time.deltaTime;
            return timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}
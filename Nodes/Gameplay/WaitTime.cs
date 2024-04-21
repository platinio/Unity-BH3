using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Wait")]
    public class WaitTime : GameplayNode
    {
        [Serialize] [Inspectable] private FloatBlackboardVariable waitTime = new();
       
        private float timer = 0.0f;

        public override string NodeName => $"Wait";

        public override void OnEnter()
        {
            timer = waitTime.GetValue(BehaviorTreeMachine);
        }

        public override ExecutionStatus OnUpdate()
        {
            timer -= Time.deltaTime;
            return timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}
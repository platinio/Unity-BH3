using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Wait")]
    public class WaitTime : GameplayNode
    {
        [Serialize] [Inspectable] private float time = 0.0f;

        private float timer = 0.0f;

        public override string NodeName => $"Wait {time} sec";

        public override void OnEnter()
        {
            timer = time;
        }

        public override ExecutionStatus OnUpdate()
        {
            timer -= Time.deltaTime;
            return timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}
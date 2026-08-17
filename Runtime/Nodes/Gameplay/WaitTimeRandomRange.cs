using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Wait Range")]
    public class WaitTimeRandomRange : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput minTime { get; private set; }
        
        [DoNotSerialize]
        public ValueInput maxTime { get; private set; }

        private float timer = 0.0f;

        public override string NodeName => "Wait Range";
        public override string Description => "Wait random random from minTime to maxTime";

        protected override void Definition()
        {
            base.Definition();
            
            minTime = ValueInput<float>(nameof(minTime));
            maxTime = ValueInput<float>(nameof(maxTime));
        }
        
        public override void OnEnter()
        {
            timer = Random.Range( minTime.GetValue<float>(), maxTime.GetValue<float>());
        }

        public override ExecutionStatus OnUpdate()
        {
            timer -= Time.deltaTime;
            return timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}
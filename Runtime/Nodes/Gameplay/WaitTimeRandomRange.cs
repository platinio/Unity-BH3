using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Flow/Wait Range")]
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
            
            // Declared with defaults, which is what makes the canvas offer inline fields and what makes the
            // typed values survive a reload. Zero is a real answer here - "wait no time" - so an unfed
            // port is not a problem to report, unlike a variable key where empty means nothing at all.
            minTime = ValueInput<float>(nameof(minTime), 0.0f);
            maxTime = ValueInput<float>(nameof(maxTime), 0.0f);
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
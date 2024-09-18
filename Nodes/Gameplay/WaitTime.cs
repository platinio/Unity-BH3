using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Gameplay/Wait")]
    public class WaitTime : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Time { get; private set; }
       
        private float timer = 0.0f;

        public override string NodeName => "Wait";

        protected override void Definition()
        {
            base.Definition();
            
            Time = ValueInput<float>(nameof(Time));
        }
        
        public override void OnEnter()
        {
            timer = (float) Time.GetValue();
        }

        public override ExecutionStatus OnUpdate()
        {
            timer -= UnityEngine.Time.deltaTime;
            return timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}
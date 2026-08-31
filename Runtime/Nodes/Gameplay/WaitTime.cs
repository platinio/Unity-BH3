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
            
            // Declared with a default, which is what makes the canvas offer an inline field and what makes
            // the typed value survive a reload. Zero is a real answer here - "wait no time", a node that
            // completes on its first update - so an unfed port is not a problem to report, unlike a
            // variable key where empty means nothing at all.
            Time = ValueInput<float>(nameof(Time), 0.0f);
        }
        
        public override void OnEnter()
        {
            timer = Time.GetValue<float>();
        }

        public override ExecutionStatus OnUpdate()
        {
            timer -= UnityEngine.Time.deltaTime;
            return timer > 0 ? ExecutionStatus.Running : ExecutionStatus.Success;
        }
    }
}
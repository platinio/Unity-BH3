using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Debug/Debug Log")]
    public class DebugLog : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput LogText { get; private set; }

        public override string NodeName => "Debug Log";

        protected override void Definition()
        {
            base.Definition();

            LogText = ValueInput<string>(nameof(LogText), string.Empty);
        }

        public override ExecutionStatus OnUpdate()
        {
            Debug.Log(LogText.GetValue());
            return ExecutionStatus.Success;
        }
    }
}


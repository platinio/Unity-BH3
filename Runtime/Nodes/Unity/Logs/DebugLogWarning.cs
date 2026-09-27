using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Debug/Debug Log Warning")]
    public class DebugLogWarning : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput LogText { get; private set; }

        public override string NodeName => "Debug Log Warning";

        protected override void Definition()
        {
            base.Definition();

            LogText = ValueInput<string>(nameof(LogText), string.Empty).AcceptsAnySource();
        }

        public override ExecutionStatus OnUpdate()
        {
            Debug.LogWarning(LogText.GetValue());
            return ExecutionStatus.Success;
        }

    }
}
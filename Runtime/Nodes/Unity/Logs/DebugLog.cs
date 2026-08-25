using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Logs/Debug Log")]
    public class DebugLog : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput LogText { get; private set; }

        public override string NodeName => "Debug Log";

        protected override void Definition()
        {
            base.Definition();

            LogText = ValueInput<object>(nameof(LogText), string.Empty);
        }

        // Migrated (spec 07 step 3). Stateless already, so the whole migration is reading the port through
        // the context instead of off the port object -- and the agent comes from the context too, so the
        // log line points at the right object on a shared tree.
        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            Debug.Log(ctx.GetValue(LogText), ctx.gameObject);
            return ExecutionStatus.Success;
        }
    }
}


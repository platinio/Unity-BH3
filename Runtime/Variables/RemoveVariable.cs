using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Variables/Remove Variable")]
    public class RemoveVariable : GameplayNode
    {
        [Serialize, Inspectable] private BehaviorTreeVariableKind VariableKind;

        [DoNotSerialize]
        public ValueInput Key { get; private set; }

        public override string NodeName => "Remove Variable";

        protected override void Definition()
        {
            base.Definition();

            // Declared with a default so the canvas offers the inline field; VariableKeyPort keeps the
            // forgotten-key failure as loud as the bare port used to.
            Key = ValueInput<string>(nameof(Key), null);
        }

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);
            VariableKeyPort.CollectProblems(Key, into);
            VariableKindField.CollectProblems(VariableKind, into);
        }

        /// <summary>
        /// Failure means there was no store to remove from — a Graph removal on a graph running without a
        /// scope, which is the only reachable case. Removing a key that was already gone is Success: that is
        /// the state the node was asked for.
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            string key = VariableKeyPort.Resolve(Key, NodeName);
            var kind = VariableKindField.Resolve(VariableKind, NodeName);

            return EraseVariable(key, kind) ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }
    }
}

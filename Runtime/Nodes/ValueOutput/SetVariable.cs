using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Variables/Set Variable")]
    public class SetVariable : GameplayNode
    {
        [Serialize, Inspectable] private BehaviorTreeVariableKind VariableKind;
      
        [DoNotSerialize]
        public ValueInput Key { get; private set; }
        
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        public override string NodeName => "Set Variable";

        protected override void Definition()
        {
            base.Definition();

            // Declared with a default so the canvas offers the inline field; VariableKeyPort keeps the
            // forgotten-key failure as loud as the bare port used to.
            Key = ValueInput<string>(nameof(Key), null);
            Value = ValueInput<object>(nameof(Value));
        }

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);
            VariableKeyPort.CollectProblems(Key, into);
            VariableKindField.CollectProblems(VariableKind, into);
        }

        public override ExecutionStatus OnUpdate()
        {
            string key = VariableKeyPort.Resolve(Key, NodeName);
            var kind = VariableKindField.Resolve(VariableKind, NodeName);
            object value = Value.GetValue();

            // Same switch as GameplayNode.SaveVariable, which is why this defers to it: two copies of the
            // rule about where a Graph write lands is exactly one copy too many.
            SaveVariable(key, kind, value);

            return ExecutionStatus.Success;
        }
    }
}
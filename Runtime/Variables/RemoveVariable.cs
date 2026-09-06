using System.Collections.Generic;
using System.Reflection;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Remove Variable")]
    public class RemoveVariable : GameplayNode
    {
        [Serialize, Inspectable] private BehaviorTreeVariableKind VariableKind;
        
        [DoNotSerialize]
        public ValueInput Key { get; private set; }

        public override string NodeName => "Remove Variable";

        private VariableDeclarationCollection variableDeclarationCollection;
        
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
        }

        public override void OnEnter()
        {
            if (variableDeclarationCollection != null) return;
            
            FieldInfo collectionProperty = typeof(VariableDeclarations).GetField("collection", BindingFlags.NonPublic | BindingFlags.Instance);
            variableDeclarationCollection = collectionProperty.GetValue(BehaviorTreeMachine.Variables.declarations) as VariableDeclarationCollection;
        }

        public override ExecutionStatus OnUpdate()
        {
            string key = VariableKeyPort.Resolve(Key, NodeName);
            variableDeclarationCollection.Remove(key);
            
            return ExecutionStatus.Success;
        }
    }
}
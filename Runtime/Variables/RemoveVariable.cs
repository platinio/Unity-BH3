using System.Reflection;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Remove Variable")]
    public class RemoveVariable : GameplayNode
    {
        [Serialize, Inspectable] private VariableKind VariableKind;
        
        [DoNotSerialize]
        public ValueInput Key { get; private set; }

        public override string NodeName => "Remove Variable";

        private VariableDeclarationCollection variableDeclarationCollection;
        
        protected override void Definition()
        {
            base.Definition();
            Key = ValueInput<string>(nameof(Key));
        }

        public override void OnEnter()
        {
            if (variableDeclarationCollection != null) return;
            
            FieldInfo collectionProperty = typeof(VariableDeclarations).GetField("collection", BindingFlags.NonPublic | BindingFlags.Instance);
            variableDeclarationCollection = collectionProperty.GetValue(BehaviorTreeMachine.Variables.declarations) as VariableDeclarationCollection;
        }

        public override ExecutionStatus OnUpdate()
        {
            string key = Key.GetValue<string>();
            variableDeclarationCollection.Remove(key);
            
            return ExecutionStatus.Success;
        }
    }
}
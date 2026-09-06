using System.Collections.Generic;
using System.Reflection;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Remove Variable")]
    public class RemoveVariable : GameplayNode
    {
        /// <summary>
        /// <see cref="VariableDeclarations"/> can define a name and answer for one, but not undefine it —
        /// the backing collection is private and there is no public removal. Looked up once, statically:
        /// the reflection is the expensive half and the field never changes.
        /// </summary>
        private static readonly FieldInfo DeclarationCollectionField =
            typeof(VariableDeclarations).GetField("collection", BindingFlags.NonPublic | BindingFlags.Instance);

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

        public override ExecutionStatus OnUpdate()
        {
            string key = VariableKeyPort.Resolve(Key, NodeName);
            var kind = VariableKindField.Resolve(VariableKind, NodeName);

            // Resolved per run rather than cached on entry, which is what this node used to do. The store a
            // Graph removal acts on is the scope the node is running under, and the same node is handed a
            // different scope at a different call site — a cached collection would go on removing from
            // whichever branch entered first.
            var declarations = DeclarationsFor(kind);
            if (declarations == null) return ExecutionStatus.Success;

            var collection = DeclarationCollectionField?.GetValue(declarations) as VariableDeclarationCollection;
            collection?.Remove(key);

            return ExecutionStatus.Success;
        }
    }
}

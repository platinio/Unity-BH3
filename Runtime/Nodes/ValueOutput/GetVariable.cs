using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Variables/Get Variable")]
    public class GetVariable : Literal
    {
        [Serialize, Inspectable] private VariableKind VariableKind;
        
        [DoNotSerialize]
        public ValueInput Key { get; private set; }
        
        [DoNotSerialize]
        public ValueOutput Value { get; private set; }

        public override string NodeName => string.IsNullOrEmpty(NodeComment)? "Get Variable" : NodeComment;

        protected override void Definition()
        {
            base.Definition();

            Key = ValueInput<string>(nameof(Key));
            Value = ValueOutput<object>(nameof(Value), () => GetValue(Key.GetValue<string>(), BehaviorTreeMachine));
        }
        
        public object GetValue(string key, BehaviorTreeMachine machine)
        {
            switch (VariableKind)
            {
                case VariableKind.Graph:
                    return GetValueFromGraph(key, machine);
                case VariableKind.Object:
                    return GetValueFromObject(key, machine);
                case VariableKind.Scene:
                    return GetValueFromScene(key);
                case VariableKind.Application:
                    return GetValueFromApp(key);
                case VariableKind.Saved:
                    return GetValueFromSaved(key);
            }
           
            return default;
        }
       
        /// <summary>
        /// Reads through the scope chain: a node inside a branch sees that branch's values first and its
        /// caller's underneath, out to the agent.
        /// <para>
        /// Going straight to <c>machine.GraphInstance.declarations</c> reads the <em>root</em> tree
        /// regardless of which sub-tree the node lives in, which breaks the half of parameter passing that
        /// makes it worth having: a caller's argument is written into the branch's own scope, so a branch
        /// reading its own parameter found nothing there and threw. It also made the node asymmetric with
        /// itself, since <c>GameplayNode.SaveVariable</c> writes Graph scope through
        /// <c>VariableScope</c> — a branch could write a value it could not then read back.
        /// </para>
        /// <para>
        /// The fallback keeps a node with no scope — a graph loaded without one — behaving exactly as it did
        /// before. <c>Get</c> walks the chain rather than flattening it, because a single read has no reason
        /// to build a merged collection.
        /// </para>
        /// </summary>
        private object GetValueFromGraph(string key, BehaviorTreeMachine machine)
        {
            var scope = VariableScope;
            if (scope != null) return scope.Get(key);

            return machine.GraphInstance.declarations.Get(key);
        }
        
        private object GetValueFromObject(string key, IGraphMachine machine)
        {
            return machine.Variables.declarations.Get(key);
        }


        private object GetValueFromScene(string key)
        {
            var variables =  SceneVariables.Instance(SceneManager.GetActiveScene());
            return variables.variables.declarations.Get(key);
        }

        private object GetValueFromApp(string key)
        {
            return ApplicationVariables.current.Get(key);
        }

        private object GetValueFromSaved(string key)
        {
            return SavedVariables.current.Get(key);
        }
       
    }
}


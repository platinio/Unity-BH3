using System;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Visual Scripting/Script Graph Variable")]
    public class VisualScriptGraphVariable : BaseVisualScriptingNode
    {
        [Serialize] [Inspectable] private BTScriptGraphVariable ScriptGraphVariable = null;
        public override string NodeName => comment == string.Empty? "Script Graph Variable" : comment;
        public override bool CanBeUsedAsTransitionDestination => false;
        public override string Description => "Returns value from Script Graph";

        [DoNotSerialize]
        public ValueOutput Output { get; private set; }

        public override bool DrawInSubTree => false;

        /// <summary>
        /// Points this node at the graph that produces its value. Only useful after the node has been added
        /// to a graph, since <see cref="Definition"/> is what creates the variable this assigns into.
        /// Needed to author a node's Visual Scripting from code.
        /// </summary>
        public void SetScriptGraph(ScriptGraphAsset asset)
        {
            ScriptGraphVariable?.SetScriptGraphAsset(asset);
        }
        
        protected override void Definition()
        {
            base.Definition();

            if (ScriptGraphVariable == null) ScriptGraphVariable = CreateGraphWithOutput(typeof(object));
            Output = ValueOutput<object>(nameof(Output), () =>
            {
                try
                {
                    runtimeException = null;
                    return ScriptGraphVariable.GetValue<object>(gameObject, ScriptGraphVariables);
                }
                catch (Exception e)
                {
                    LastExecutionStatus = ExecutionStatus.Exception;
                    runtimeException = e;
                    throw;
                }
            });
        }

        public override ExecutionStatus OnUpdate()
        {
            if (runtimeException != null) return ExecutionStatus.Exception;
            return base.OnUpdate();
        }
    }
}
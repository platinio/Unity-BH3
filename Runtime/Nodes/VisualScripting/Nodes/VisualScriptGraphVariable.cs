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
        
        protected override void Definition()
        {
            base.Definition();

            if (ScriptGraphVariable == null) ScriptGraphVariable = CreateGraphWithOutput(typeof(object));
            Output = ValueOutput<object>(nameof(Output), () =>
            {
                try
                {
                    runtimeException = null;
                    return ScriptGraphVariable.GetValue<object>(gameObject, BehaviorTreeMachine.GraphInstance.declarations);
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
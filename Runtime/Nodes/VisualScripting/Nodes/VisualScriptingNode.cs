using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Visual Scripting/Script Graph")]
    public class VisualScriptingNode : BaseVisualScriptingNode
    {
        [Serialize] [Inspectable] private BTScriptGraphVariable OnAwakeGraph = null;
        [Serialize] [Inspectable] private BTScriptGraphVariable OnEnterGraph = null;
        [Serialize] [Inspectable] private BTScriptGraphVariable OnUpdateGraph = null;
        [Serialize] [Inspectable] private BTScriptGraphVariable OnExitGraph = null;

        public override string NodeName => comment == string.Empty? "Script Graph" : comment;
        public override bool CanBeUsedAsTransitionDestination => true;

        protected override void Definition()
        {
            base.Definition();

            if (OnAwakeGraph == null) OnAwakeGraph = CreateRunnableScriptGraphVariable();
            if (OnEnterGraph == null) OnEnterGraph = CreateRunnableScriptGraphVariable();
            if (OnUpdateGraph == null) OnUpdateGraph = CreateGraphWithOutput(typeof(ExecutionStatus));
            if (OnExitGraph == null) OnExitGraph = CreateRunnableScriptGraphVariable();
        }

        public override void OnAwake()
        {
            if (OnAwakeGraph?.ScriptGraphAsset == null) return;
            OnAwakeGraph.Run(gameObject, BehaviorTreeMachine.GraphInstance.declarations);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (OnEnterGraph?.ScriptGraphAsset == null) return;
            OnEnterGraph?.Run(gameObject, BehaviorTreeMachine.GraphInstance.declarations);
        }

        public override ExecutionStatus OnUpdate()
        {
            if (OnUpdateGraph?.ScriptGraphAsset == null) return ExecutionStatus.Success;
            return OnUpdateGraph.GetValue<ExecutionStatus>(gameObject, BehaviorTreeMachine.GraphInstance.declarations);
        }

        public override void OnExit()
        {
            base.OnExit();
            if (OnExitGraph?.ScriptGraphAsset == null) return;
            OnExitGraph?.Run(gameObject, BehaviorTreeMachine.GraphInstance.declarations);
        }
    }
}


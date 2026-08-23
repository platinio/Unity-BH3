using System.Collections.Generic;
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

        /// <summary>
        /// The four lifecycle graphs, for tooling that has to see every graph this node reads.
        ///
        /// <para>
        /// Exposed because these fields can hold a <b>Function</b> — the inspector drawer is registered for
        /// <c>BTScriptGraphVariable</c>, which is what all four are — and a Function here has no way to be
        /// given arguments: only <see cref="VisualScriptGraphVariable"/> declares ports from a contract. That
        /// makes the state worth reporting, and verification cannot report what it cannot reach.
        /// </para>
        /// </summary>
        public IEnumerable<ScriptGraphVariable> LifecycleGraphs
        {
            get
            {
                yield return OnAwakeGraph;
                yield return OnEnterGraph;
                yield return OnUpdateGraph;
                yield return OnExitGraph;
            }
        }

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
            if (OnAwakeGraph == null || !OnAwakeGraph.HasGraph) return;

            // Each of these four announces this node for the duration of the graph it runs, so a
            // Set BT Variable unit inside any of them is attributed to a node the canvas can point at
            // rather than to a bare name. The push and pop compile out with the recorder.
            Debugging.BehaviorTreeRecorder.PushScriptGraphOwner(this);

            try { OnAwakeGraph.Run(gameObject, ScriptGraphVariables); }
            finally { Debugging.BehaviorTreeRecorder.PopScriptGraphOwner(); }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (OnEnterGraph == null || !OnEnterGraph.HasGraph) return;

            Debugging.BehaviorTreeRecorder.PushScriptGraphOwner(this);

            try { OnEnterGraph.Run(gameObject, ScriptGraphVariables); }
            finally { Debugging.BehaviorTreeRecorder.PopScriptGraphOwner(); }
        }

        public override ExecutionStatus OnUpdate()
        {
            if (OnUpdateGraph == null || !OnUpdateGraph.HasGraph) return ExecutionStatus.Success;

            Debugging.BehaviorTreeRecorder.PushScriptGraphOwner(this);

            try { return OnUpdateGraph.GetValue<ExecutionStatus>(gameObject, ScriptGraphVariables); }
            finally { Debugging.BehaviorTreeRecorder.PopScriptGraphOwner(); }
        }

        public override void OnExit()
        {
            base.OnExit();
            if (OnExitGraph == null || !OnExitGraph.HasGraph) return;

            Debugging.BehaviorTreeRecorder.PushScriptGraphOwner(this);

            try { OnExitGraph.Run(gameObject, ScriptGraphVariables); }
            finally { Debugging.BehaviorTreeRecorder.PopScriptGraphOwner(); }
        }
    }
}


using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using GraphReference = Unity.VisualScripting.GraphReference;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Runs a script graph, badly, and is kept only for the trees that already contain one.
    ///
    /// <para>
    /// It builds a <c>Flow</c> from the <em>shared asset's</em> reference rather than a per-agent instance,
    /// and outside the machine's variable scope. So graph-scoped state inside that script graph is shared by
    /// every agent running it, <c>Self</c> does not resolve to the agent, and a flow that keeps running
    /// outlives the node with nothing able to cancel it. <c>OnUpdate</c> is the inherited default, so the
    /// node reports Success the same tick whatever the graph did.
    /// </para>
    ///
    /// <para>
    /// <b>Use <see cref="VisualScriptingNode"/> instead</b>, which does all of this properly. This node is
    /// not deleted because the class name is what assets serialize, so removing it would break every tree
    /// holding one; it is marked in the create menu and reports itself on the canvas so no new tree picks it
    /// up. The typo in the serialized field name is left alone for the same reason.
    /// </para>
    /// </summary>
    [GraphCreateMenu("Unity/Visual Scripting/Run Script Graph (Deprecated)")]
    public class RunScriptGraph : GameplayNode
    {
        [Serialize, Inspectable] private ScriptGraphAsset scripGraphAsset;

        public override string Description => "Executes ScriptGraphAsset";

        public override string NodeName 
        {
            get
            {
                if (scripGraphAsset == null) return "Missing ScriptGraph";
                return $"Run {scripGraphAsset.name}";
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // Reported rather than thrown. An unassigned asset is an authoring mistake, and every other node
            // that can have one says so on the canvas instead of taking the branch down at runtime.
            if (scripGraphAsset == null)
            {
                Debug.LogError($"'{NodeName}' has no script graph assigned, so there is nothing to run.",
                    gameObject);
                return;
            }

            var graph = scripGraphAsset.graph;
            var graphReference = scripGraphAsset.GetReference() as GraphReference;

            var flow = Flow.New(graphReference);
            
            var inputNode = GetNodeOfType<GraphInput>(graph);
            if (inputNode == null)
            {
                Debug.LogError("can't find script graph input node");
               return;
            }

            foreach (var controlOutput in inputNode.controlOutputs)
            {
                flow.Run(controlOutput);
            }
        }
        
        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            if (scripGraphAsset == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No script graph assigned, so this node runs nothing.",
                    "Assign one, or replace this node with a Visual Scripting node."));
            }

            // Unconditional, because the node is wrong even when it is fully configured -- see the class
            // summary. A warning on every instance is the point: it is how the ones already in trees get
            // found.
            into.Add(new NodeProblem(NodeProblemSeverity.Warning,
                "Run Script Graph executes the shared asset with no agent context, so graph state is shared "
                + "between agents and Self does not resolve.",
                "Replace it with a Visual Scripting node."));
        }

        private T GetNodeOfType<T>(FlowGraph flowGraph)
        {
            foreach (var unit in flowGraph.units)
            {
                if (unit is T node)
                {
                    return node;
                }
            }

            return default;
        }
    }
}
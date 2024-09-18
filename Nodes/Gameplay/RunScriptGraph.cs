using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Visual Scripting/Run Script Graph")]
    public class RunScriptGraph : GameplayNode
    {
        [Serialize, Inspectable] private ScriptGraphAsset scripGraphAsset;

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
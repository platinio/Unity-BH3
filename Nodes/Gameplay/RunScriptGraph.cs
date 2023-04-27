using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Gameplay/Run Script Graph")]
    public class RunScriptGraph : GameplayNode
    {
        [Serialize] private ScriptGraphAsset m_scripGraphAsset;

        public override void OnEnter()
        {
            var graph = m_scripGraphAsset.graph;
            var graphReference = m_scripGraphAsset.GetReference() as GraphReference;

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
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [CreateAssetMenu(menuName = "Visual Scripting/Behavior Tree", fileName = "New Behavior Tree Graph", order = 81)]
    public class BehaviorTreeGraphAsset : BaseGraphAsset<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        [Serialize, Inspectable]
        public VariableDeclarations declarations { get; internal set; } = new() { Kind = VariableKind.Graph };
        
        //[ContextMenu("Show Data...")]
        protected override void ShowData()
        {
            base.ShowData();
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return BehaviorTreeGraph.CreateEmpty();
        }
    }
}
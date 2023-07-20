using Platinio.GraphCore;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [CreateAssetMenu(menuName = "Visual Scripting/Behavior Tree", fileName = "New Behavior Tree Graph", order = 81)]
    public class BehaviorTreeGraphAsset : BaseGraphAsset<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        [ContextMenu("Show Data...")]
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
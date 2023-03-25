using Platinio.GraphCore;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    [CreateAssetMenu(menuName = "Visual Scripting/Behaviour Tree", fileName = "New Behaviour Tree Graph", order = 81)]
    public class BehaviourTreeGraphAsset : BaseGraphAsset<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        [ContextMenu("Show Data...")]
        protected override void ShowData()
        {
            base.ShowData();
        }

        public override BehaviourTreeGraph DefaultGraph()
        {
            return BehaviourTreeGraph.CreateEmpty();
        }
    }
}
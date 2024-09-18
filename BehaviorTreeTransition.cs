using System.Collections.Generic;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    public class BehaviorTreeTransition : BaseGraphTransition<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public List<PlaceHolderNode> PlaceHolderNodes;

        public BehaviorTreeTransition()
        {
        }
    }
}
using System.Collections.Generic;
using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    public class BehaviorTreeTransition : BaseGraphTransition<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public List<PlaceHolderNode> PlaceHolderNodes;

        public BehaviorTreeTransition()
        {
        }
    }
}
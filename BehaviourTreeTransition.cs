using System.Collections.Generic;
using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    public class BehaviourTreeTransition : BaseGraphTransition<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        public List<PlaceHolderNode> PlaceHolderNodes;

        public BehaviourTreeTransition()
        {
        }
    }
}
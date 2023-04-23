using Platinio.AI;
using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    /// <summary>
    /// Base class for all behaviour tree nodes
    /// </summary>
    public class BehaviourTreeNode : BaseGraphNode<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        public virtual DecisionScoreResult CalculateScore()
        {
            return default;
        }

        public virtual void OnTraverse(DecisionScoreResult decisionScoreResult) { }
    }

}


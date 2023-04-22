using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    /// <summary>
    /// Base class for all behaviour tree nodes
    /// </summary>
    public class BehaviourTreeNode : BaseGraphNode<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        public virtual float CalculateScore(out int bestVariableScoreIndex)
        {
            bestVariableScoreIndex = 0;
            return 0.0f;
        }

        public virtual void OnTraverse(int bestScoreIndex) { }
    }

}


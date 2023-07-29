using Platinio.Considerations;
using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// Base class for all behavior tree nodes
    /// </summary>
    public class BehaviorTreeNode : BaseGraphNode<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        public virtual DecisionScoreResult CalculateScore()
        {
            return default;
        }

        public virtual void OnTraverse(DecisionScoreResult decisionScoreResult) { }
    }

}


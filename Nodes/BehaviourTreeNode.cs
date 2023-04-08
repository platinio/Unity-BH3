using System.Collections.Generic;
using Platinio.AI;
using Platinio.GraphCore;
using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    /// <summary>
    /// Base class for all behaviour tree nodes
    /// </summary>
    public class BehaviourTreeNode : BaseGraphNode<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        [Serialize] [Inspectable] protected List<ScoredCondition> m_scoredConditions;

        private AIScoreEvaluator m_scoreEvaluator = new();

        public float CalculateScore()
        {
            return m_scoreEvaluator.CalculateScore(Machine.VariableDeclarations, m_scoredConditions);
        }
    }

}


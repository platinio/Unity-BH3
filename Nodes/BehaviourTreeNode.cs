using System.Collections.Generic;
using Platinio.AI;
using Platinio.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace Platinio.BehaviourTree
{
    /// <summary>
    /// Base class for all behaviour tree nodes
    /// </summary>
    public class BehaviourTreeNode : BaseGraphNode<BehaviourTreeGraph, BehaviourTreeNode, BehaviourTreeTransition>
    {
        [Serialize] [Inspectable] protected ConditionScorePackage m_scoredConditions;

        private AIScoreEvaluator m_scoreEvaluator = new();

        public float CalculateScore(out int bestVariableScoreIndex)
        {
            return m_scoreEvaluator.CalculateScore(Machine.VariableDeclarations, m_scoredConditions.ScoredConditions, out bestVariableScoreIndex);
        }

        public void OnTraverse(int bestScoreIndex)
        {
            m_scoredConditions.OnTraverse(Machine.VariableDeclarations, bestScoreIndex);
        }
    }

}


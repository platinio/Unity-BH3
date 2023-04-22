using Platinio.AI;
using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    public class Composite : ContainerNode
    {
        [Serialize] [Inspectable] protected ConditionScorePackage m_scoredConditions;

        private AIScoreEvaluator m_scoreEvaluator = new();
        
        protected int m_currentExecutingChildIndex = 0;
       
        public override void OnAwake()
        {
            m_currentExecutingChildIndex = 0;
        }

        protected void MoveCurrentExecutingChildIndex()
        {
            
        }
        
        public override float CalculateScore(out int bestVariableScoreIndex)
        {
            return m_scoreEvaluator.CalculateScore(Machine.VariableDeclarations, m_scoredConditions.ScoredConditions, out bestVariableScoreIndex);
        }

        public override void OnTraverse(int bestScoreIndex)
        {
            m_scoredConditions.OnTraverse(Machine.VariableDeclarations, bestScoreIndex);
        }
    }
}
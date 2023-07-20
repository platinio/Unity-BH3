using Platinio.AI;
using Platinio.Share;
using ScriptableObjectDatabase;
using Unity.VisualScripting;

namespace Platinio.BehaviorTree
{
    public class Composite : ContainerNode
    {
        [Serialize] [Inspectable] [ScriptableItemDatabaseSelector(typeof(ScriptableDecisionDatabase))]
        protected ScriptableDecision m_scriptableDecision;

        protected int m_currentExecutingChildIndex = 0;
       
        public override void OnAwake()
        {
            m_currentExecutingChildIndex = 0;
        }

        protected void MoveCurrentExecutingChildIndex()
        {
            
        }
        
        public override DecisionScoreResult CalculateScore()
        {
            return m_scriptableDecision.Evaluate(Machine.gameObject.GetComponent<ICharacterEntity>());
        }
    }
}
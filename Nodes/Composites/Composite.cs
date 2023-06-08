using Platinio.AI;
using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    public class Composite : ContainerNode
    {
        [Serialize] [Inspectable] protected ScriptableDecision m_scriptableDecision;

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
            return m_scriptableDecision.Evaluate(Machine.gameObject.GetComponent<IActor>());
        }
    }
}
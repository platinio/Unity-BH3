namespace Platinio.BehaviourTree
{
    public class Composite : ContainerNode
    {
        protected int m_currentExecutingChildIndex = 0;
       
        public override void OnAwake()
        {
            m_currentExecutingChildIndex = 0;
        }

        protected void MoveCurrentExecutingChildIndex()
        {
            
        }
    }
}
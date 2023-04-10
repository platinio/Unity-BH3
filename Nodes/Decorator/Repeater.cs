using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Decorator/Create Repeater")]
    public class Repeater : Decorator
    {
        protected override string NodeIconPath => "NodeIcons/Cycle";
        public override string NodeName => "Repeater";
        public override bool CanExecute => true;
        
        private int m_currentExecutingChildIndex = 0;

        public override void OnEnter()
        {
            base.OnEnter();
            m_currentExecutingChildIndex = 0;
        }

        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count <= 0) return ExecutionStatus.Success; 
            
            var task = GetChildren()[m_currentExecutingChildIndex];
            var result = task.OnUpdate();

            if (result == ExecutionStatus.Failure || result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                m_currentExecutingChildIndex++;
                
                if (GetChildren().Count <= m_currentExecutingChildIndex)
                {
                    m_currentExecutingChildIndex = 0;
                }

                GetChildren()[m_currentExecutingChildIndex].OnNodeEnter();
            }

            return ExecutionStatus.Running;

        }
    }
}
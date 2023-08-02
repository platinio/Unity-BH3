using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Selector")]
    public class Selector : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Selector";
        public override string NodeName => "Selector";

        public override void OnEnter()
        {
            m_currentExecutingChildIndex = 0;
            
            if (GetChildren().Count == 0) return;
            GetChildren()[0].OnNodeEnter();
        }
        
        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0) return ExecutionStatus.Success;

            var task = GetChildren()[m_currentExecutingChildIndex];
            var result = task.OnUpdate();

            if (result == ExecutionStatus.Success)
            {
                task.OnNodeExit();
                return ExecutionStatus.Success;
            }
            if (result == ExecutionStatus.Failure)
            {
                task.OnNodeExit();
               
                m_currentExecutingChildIndex++;
                if (GetChildren().Count <= m_currentExecutingChildIndex) return ExecutionStatus.Failure;
                
                GetChildren()[m_currentExecutingChildIndex].OnNodeEnter();
                return ExecutionStatus.Running;
            }

            return result;
        }

        public override void OnExit()
        {
            m_currentExecutingChildIndex = 0;
        }
    }
}
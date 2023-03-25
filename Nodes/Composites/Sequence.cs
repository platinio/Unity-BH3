using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    [GraphCreateMenu("Composite/Create Sequence")]
    public class Sequence : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Sequence";
        public override string NodeName => "Sequence";

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
                m_currentExecutingChildIndex++;
                if (GetChildren().Count <= m_currentExecutingChildIndex) return ExecutionStatus.Success;
                
                GetChildren()[m_currentExecutingChildIndex].OnNodeEnter();
                return ExecutionStatus.Running;
            }
            if (result == ExecutionStatus.Failure)
            {
                task.OnNodeExit();
                m_currentExecutingChildIndex = 0;
                return ExecutionStatus.Failure;
            }

            return result;
        }

        public override void OnExit()
        {
            m_currentExecutingChildIndex = 0;
        }
    }
}
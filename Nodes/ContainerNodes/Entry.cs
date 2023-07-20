using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// entry point for a behavior tree graph
    /// </summary>
    public class Entry : ContainerNode
    {
        protected override string NodeIconPath => "NodeIcons/Entry";
        public override string NodeName => "Entry";

        public override bool CanDelete => false;
        public override int MaxChildren => 1;

        private bool m_isComplete = false;
        
        public override void OnEnter()
        {
            m_isComplete = false;
            foreach (var children in GetChildren())
            {
                children.OnNodeEnter();
            }
        }

        public override void OnExit()
        {
            foreach (var children in GetChildren())
            {
                children.OnNodeExit();
            }
        }

        public override ExecutionStatus OnUpdate()
        {
            if (m_isComplete) return ExecutionStatus.Success;
            
            if (CanExecute)
            {
                if (GetChildren().Count == 0)
                {
                    m_isComplete = true;
                    return ExecutionStatus.Success;
                }
                
                var result =  GetChildren()[0].OnUpdate();
                m_isComplete = result == ExecutionStatus.Success || result == ExecutionStatus.Failure;
            }

            return ExecutionStatus.Running;
        }
    }
}
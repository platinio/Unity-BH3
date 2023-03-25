using Platinio.GraphCore;

namespace Platinio.BehaviourTree
{
    /// <summary>
    /// entry point for a behaviour tree graph
    /// </summary>
    public class Entry : ContainerNode
    {
        protected override string NodeIconPath => "NodeIcons/Entry";
        public override string NodeName => "Entry";

        public override bool CanDelete => false;
        public override int MaxChildren => 1;
        
        public override void OnEnter()
        {
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
            if (CanExecute)
            {
                if (GetChildren().Count == 0) return ExecutionStatus.Success;
                return GetChildren()[0].OnUpdate();
            }

            return ExecutionStatus.Failure;
        }
    }
}
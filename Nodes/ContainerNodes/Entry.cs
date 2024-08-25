using Platinio.GraphCore;

namespace Platinio.BehaviorTree
{
    /// <summary>
    /// entry point for a behavior tree graph
    /// </summary>
    public class Entry : ContainerNode
    {
        public override string NodeName => "Entry";
        public override bool CanDelete => false;
        public override int MaxChildrenLimit => 1;
        protected override string NodeIconPath => "NodeIcons/Entry";
        public override bool CanUseConditionalExecutions => false;

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
            if (GetChildren().Count == 0)
            {
                return ExecutionStatus.Success;
            }
                
            return GetChildren()[0].OnUpdateInternal();
        }
    }
}
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
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

        public override bool CanCopy => false;
        public override bool CanDuplicate => false;
        public override bool CanCut => false;
        public override bool CanBeUseAsTransitionDestination => false;

        public override void OnEnter()
        {
            for (int i = 0; i < GetChildren().Count; i++)
            {
                GetChildren()[i].OnNodeEnter();
            }
        }

        public override void OnExit()
        {
            for (int i = 0; i < GetChildren().Count; i++)
            {
                GetChildren()[i].OnNodeExit();
            }
        }

        public override ExecutionStatus OnUpdate()
        {
            if (GetChildren().Count == 0)
            {
                return ExecutionStatus.Success;
            }
                
            var status = GetChildren()[0].OnUpdateInternal();
            return status;
        }
    }
}
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
        public override bool CanBeUsedAsTransitionDestination => false;

        public override void OnExit(BTContext ctx)
        {
            for (int i = 0; i < ctx.ChildCount; i++)
            {
                ctx.ExitChild(i);
            }
        }

        /// <summary>
        /// Reports the tree below verbatim. The child is entered here rather than in <c>OnEnter</c>: the
        /// machine enters the root once and then ticks it for the lifetime of the agent, so an entry refused
        /// at start-up would otherwise never be retried — see <see cref="BTContext.TickChild"/>.
        /// </summary>
        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            if (ctx.ChildCount == 0)
            {
                return ExecutionStatus.Success;
            }

            return ctx.TickChild(0);
        }
    }
}
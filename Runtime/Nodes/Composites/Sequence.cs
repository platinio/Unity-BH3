using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Composite/Create Sequence")]
    public class Sequence : Composite
    {
        protected override string NodeIconPath => "NodeIcons/Sequence";
        public override string NodeName => "Sequence";

        public override string Description => "Executes child nodes in order from left to right.\nExecution ends when any child node returns FAILURE.";

        public override void OnEnter(BTContext ctx) => ctx.Memory<CompositeMemory>().Current = 0;

        /// <inheritdoc cref="Selector.OnUpdate(BTContext)"/>
        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            var memory = ctx.Memory<CompositeMemory>();

            if (ctx.ChildCount == 0) return ExecutionStatus.Success;

            while (memory.Current < ctx.ChildCount)
            {
                var result = ctx.TickChild(memory.Current);

                if (result == ExecutionStatus.Success)
                {
                    memory.Current++;
                    continue;
                }

                if (result == ExecutionStatus.Failure)
                {
                    memory.Current = 0;
                    return ExecutionStatus.Failure;
                }

                return result;
            }

            memory.Current = 0;
            return ExecutionStatus.Success;
        }

        /// <inheritdoc cref="Selector.OnExit(BTContext)"/>
        public override void OnExit(BTContext ctx)
        {
            for (int i = 0; i < ctx.ChildCount; i++) ctx.ExitChild(i);

            ctx.Memory<CompositeMemory>().Current = 0;
        }
    }
}

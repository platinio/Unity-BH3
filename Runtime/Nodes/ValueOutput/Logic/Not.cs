using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Inverts a boolean. Guards stack as an AND — a node runs only when every
    /// <see cref="ConditionalExecution"/> naming it evaluates true — so this is what expresses the other
    /// half: a branch that must stand down while some higher-priority state is active guards itself on
    /// Not(that state) rather than relying on branch order.
    /// </summary>
    [GraphCreateMenu("Logic/Not")]
    public class Not : GameplayNode
    {
        [DoNotSerialize]
        public ValueInput Value { get; private set; }

        [DoNotSerialize]
        public ValueOutput Result { get; private set; }

        public override string NodeName => "Not";

        public override string Description => "Outputs the inverse of Value.";

        public override bool CanBeUsedAsTransitionDestination => false;

        protected override void Definition()
        {
            base.Definition();

            Value = ValueInput<bool>(nameof(Value), false);

            Result = ValueOutput<bool>(nameof(Result), () => !Value.GetValue<bool>());
        }
    }
}

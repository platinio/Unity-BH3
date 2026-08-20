using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A <see cref="Selector"/> whose children are reshuffled after every pass.
    ///
    /// <para>
    /// <b>Take-over guards preempt in the shuffled order, and that is deliberate.</b> The inherited
    /// <c>TryChangeRunningChild</c> lets a higher-priority sibling claim the slot from the running one,
    /// where "higher priority" means earlier in <c>children</c> — and here that list is this pass's random
    /// order. So the same guard can preempt a given sibling on one pass and not the next. It reads like a
    /// bug and is not: a random selector's whole contract is that the order is drawn fresh each pass, and
    /// the priority within a pass is that order. Refusing preemption instead would leave a take-over guard
    /// on one of these children silently never firing, which is the worse surprise of the two.
    /// </para>
    /// </summary>
    [GraphCreateMenu("Composite/Random Selector")]
    public class RandomSelector : Selector
    {
        public override string NodeName => "Random Selector";
        protected override string NodeIconPath => "NodeIcons/RandomSelector";
        public override string Description => "Executes child nodes in random order.\nExecution ends when any child node returns SUCCESS.";

        public override void OnExit()
        {
            base.OnExit();
            SortChildren();
        }

        public override void SortChildren()
        {
            RandomChildOrder.Shuffle(GetChildren());
        }
    }
}
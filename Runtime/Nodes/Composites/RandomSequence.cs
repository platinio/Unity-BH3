using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A <see cref="Sequence"/> whose children are reshuffled after every pass.
    ///
    /// <para>
    /// <b>Unlike <see cref="RandomSelector"/>, nothing here preempts.</b> Take-over is
    /// <c>Composite.TryChangeRunningChild</c>, which defaults to returning false and is overridden only by
    /// <see cref="Selector"/> — so a sequence, random or not, runs the order it started with until a child
    /// fails. Within a pass that order is the one drawn for that pass, and it is not revisited.
    /// </para>
    ///
    /// <para>
    /// The consequence worth stating, because a designer will hit it: a take-over guard on one of these
    /// children never fires. That is the inert guard <see cref="RandomSelector"/>'s note calls the worse of
    /// the two surprises — and it is inherited from <see cref="Sequence"/> rather than caused by the
    /// shuffle, so shuffling neither creates it nor can fix it.
    /// </para>
    /// </summary>
    [GraphCreateMenu("Composite/Random Sequence")]
    public class RandomSequence : Sequence
    {
        public override string NodeName => "Random Sequence";
        protected override string NodeIconPath => "NodeIcons/RandomSequence";
        public override string Description => "Executes child nodes in random order.\nExecution ends when any child node returns FAILURE.";

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
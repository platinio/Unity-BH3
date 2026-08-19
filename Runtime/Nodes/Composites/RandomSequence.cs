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
    /// The consequence worth stating, because a designer will hit it: a guard on one of these children set
    /// to <c>Takes Over Lower Priority</c> never gets to bid. It still gates entry and still stops its own
    /// branch — only the take-over half is idle, and <c>bt_verify</c> reports it as defined but not active.
    /// This is inherited from <see cref="Sequence"/> rather than caused by the shuffle, so shuffling neither
    /// creates it nor can fix it.
    /// </para>
    ///
    /// <para>
    /// To stop a whole routine when a condition drops, guard the branch rather than one of its steps: put
    /// the reactive guard on the <c>RunBehaviorTreeGraphNode</c> that calls it, or on this node itself.
    /// Aborting a call node tears down every node inside the instance, which is the effect guarding a child
    /// is usually reaching for. See <c>docs/reactive-guards.md</c>.
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
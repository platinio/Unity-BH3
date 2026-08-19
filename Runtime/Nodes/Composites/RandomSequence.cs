using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A <see cref="Sequence"/> whose children are reshuffled after every pass.
    ///
    /// <para>
    /// The same note about preemption order applies as on <see cref="RandomSelector"/>: priority within a
    /// pass is the order drawn for that pass.
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
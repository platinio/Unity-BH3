using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A precondition that keeps watching: <em>is this still true?</em>
    ///
    /// <para>
    /// Where <see cref="ConditionalExecution"/> is a doorman — it checks once and stops caring — this is a
    /// watchman. It can throw its owner out mid-run (<see cref="AbortsOwner"/>), and it can let its owner in
    /// ahead of a lower-priority branch that is already running (<see cref="Preempts"/>). Both default on,
    /// and they are independent.
    /// </para>
    ///
    /// <para>
    /// <b>Turning <see cref="AbortsOwner"/> off is a real authoring move, not a way to disable the node.</b>
    /// It is the committed swing: Attack bids for control the moment the target comes into range, but once
    /// the animation has started it finishes even if the target steps back out. That combination —
    /// preempts, does not self-abort — is unreachable with any single flag, which is why these are two.
    /// </para>
    ///
    /// <para>
    /// This is what collapses the O(n&#178;) coupling guards used to force. When only the running branch can
    /// interrupt itself, every branch has to carry the negated preconditions of everything above it, and
    /// Idle ends up spelling out <c>Not(hasTarget) AND Not(lowHP) AND ...</c>, edited again every time a
    /// branch is added above it. Responsibility was inverted: the branch that wants to take over should
    /// carry the condition, not the branch that has to yield. With preemption, each branch states only its
    /// own precondition and the fallback carries none at all.
    /// </para>
    ///
    /// <para>
    /// <b>A guard must stay pure — it may only read.</b> That was tolerable to ignore before, because a
    /// guard only ran while its owner ran. It is not now: a reactive guard is evaluated on its own schedule,
    /// including while unrelated branches execute. A graph that finds the nearest enemy and caches it into
    /// <c>currentTarget</c> before answering would overwrite that value several times a second while another
    /// branch is mid-swing using it. Compute-and-write is a service; read-and-answer is a guard.
    /// </para>
    ///
    /// <para>
    /// Abstract for the same reason <see cref="ConditionalExecution"/> is: how a guard obtains its boolean
    /// is a separate question from when it is allowed to recompute. <see cref="BooleanReactiveGuard"/>
    /// supplies the port; everything about scheduling and capability lives here, so a later guard that reads
    /// a distance or a query result inherits all of it.
    /// </para>
    /// </summary>
    public abstract class ReactiveGuard : ConditionalExecution
    {
        [Serialize, Inspectable]
        private bool abortsOwner = true;

        [Serialize, Inspectable]
        private bool preempts = true;

        /// <inheritdoc/>
        public override bool AbortsOwner => abortsOwner;

        /// <inheritdoc/>
        public override bool Preempts => preempts;

        /// <summary>
        /// Sets both capabilities. For authoring code and tests; the designer-facing path is the two
        /// inspector fields.
        /// </summary>
        public void SetCapabilities(bool abortsOwner, bool preempts)
        {
            this.abortsOwner = abortsOwner;
            this.preempts = preempts;
        }
    }
}

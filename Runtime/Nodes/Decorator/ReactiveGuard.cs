using System.Collections.Generic;
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
        protected bool abortsOwner = true;

        [Serialize, Inspectable]
        protected bool preempts = true;

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

        /// <summary>
        /// When this guard may recompute, OR'd together. Empty means every tick — the behaviour a guard had
        /// before triggers existed, so an asset authored without them is unchanged.
        /// </summary>
        [Serialize, Inspectable]
        protected List<GuardTrigger> triggers = new();

        public IReadOnlyList<GuardTrigger> Triggers => triggers;

        public void AddTrigger(GuardTrigger trigger)
        {
            if (trigger == null) return;

            triggers ??= new List<GuardTrigger>();
            triggers.Add(trigger);
        }

        public void ClearTriggers() => triggers?.Clear();

        [DoNotSerialize] private bool hasCachedResult;
        [DoNotSerialize] private bool cachedResult;
        [DoNotSerialize] private float lastEvaluatedAt = float.NegativeInfinity;

        /// <summary>How many times this guard has actually run its condition. What the cost display reads.</summary>
        [DoNotSerialize]
        public int Evaluations { get; private set; }

        /// <summary>
        /// Recomputes only when something says the answer may have moved, and otherwise hands back what it
        /// last decided. A guard whose inputs have not changed costs one bool check rather than a graph run.
        /// </summary>
        public override bool Ask(bool fresh)
        {
            if (!fresh && hasCachedResult && !IsDue()) return cachedResult;

            cachedResult = Evaluate();
            hasCachedResult = true;
            Evaluations++;
            lastEvaluatedAt = Now;

            if (triggers != null)
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    triggers[i]?.OnEvaluated(Owner);
                }
            }

            return cachedResult;
        }

        /// <summary>Any trigger claiming the answer may have moved. No triggers means always due.</summary>
        private bool IsDue()
        {
            if (triggers == null || triggers.Count == 0) return true;

            float since = Now - lastEvaluatedAt;

            for (int i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] != null && triggers[i].IsDue(Owner, since)) return true;
            }

            return false;
        }

        /// <summary>
        /// Play-mode time, and zero outside it. An edit-mode test ticks a tree by hand with no time passing,
        /// so an interval trigger there is due exactly once — which is what makes the dirty-flag economy
        /// testable without a running scene.
        /// </summary>
        private static float Now => UnityEngine.Application.isPlaying ? UnityEngine.Time.time : 0.0f;
    }
}

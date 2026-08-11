using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A precondition attached to a node: <em>may this branch start?</em>
    ///
    /// <para>
    /// <b>Evaluated once, at entry.</b> It gates whether its owner is entered and then stops caring, which
    /// is what lets the condition behind it be arbitrarily expensive and arbitrarily opaque — a random roll,
    /// a tactical position query, a config lookup. Cost at the door does not matter.
    /// </para>
    ///
    /// <para>
    /// For a precondition that must keep holding while the branch runs, or that should let a higher-priority
    /// branch take over, use <see cref="ReactiveGuard"/>. The split falls on the axis that matters:
    /// <b>evaluated once may be arbitrary; evaluated repeatedly must be declared.</b> A guard re-checked on a
    /// schedule has to say what it depends on, or nothing can be cheap about it.
    /// </para>
    /// </summary>
    [SpecialNode]
    public abstract class ConditionalExecution : GameplayNode
    {
        [Serialize] private BehaviorTreeNode owner;

        public BehaviorTreeNode Owner => owner;

        /// <summary>
        /// Whether this guard kills its owner when it turns false mid-run. False here, so a plain
        /// <see cref="ConditionalExecution"/> is a doorman: it decides entry and never interrupts.
        ///
        /// <para>
        /// Expressed as a capability the walks filter on rather than as a type check, so a
        /// <see cref="ReactiveGuard"/> with its abort switched off correctly drops out of the update walk
        /// while still gating entry — the committed swing, where a branch bids for control but finishes what
        /// it started. A type-based filter would get exactly that case wrong.
        /// </para>
        /// </summary>
        public virtual bool AbortsOwner => false;

        /// <summary>
        /// Whether this guard can take control away from a lower-priority sibling that is already running.
        /// False here: a doorman cannot open someone else's door.
        /// </summary>
        public virtual bool Preempts => false;

        public void UpdateOwner(BehaviorTreeNode owner)
        {
            this.owner = owner;
        }

        public bool EvaluateInternal() => EvaluateInternal(fresh: false);

        public bool EvaluateInternal(bool fresh)
        {
            bool result = Ask(fresh);
            LastExecutionStatus = result ? ExecutionStatus.Success : ExecutionStatus.Failure;

            return result;
        }

        /// <summary>
        /// The answer, recomputing or not as this guard sees fit. A doorman has nothing to cache and simply
        /// evaluates; <see cref="ReactiveGuard"/> overrides this with the dirty-flag model.
        /// </summary>
        /// <param name="fresh">
        /// When true the guard must recompute regardless of what it has cached. Entry passes true, and the
        /// asymmetry is deliberate: a stale <em>false</em> costs latency, but a stale <em>true</em> enters a
        /// branch whose precondition no longer holds — the animation starts, the token is claimed, and the
        /// abort has to unwind it. Entries are rare next to ticks, so the extra evaluation buys out a whole
        /// class of visible glitch.
        /// </param>
        public virtual bool Ask(bool fresh) => Evaluate();

        public abstract bool Evaluate();

        public override void BeforeRemove()
        {
            base.BeforeRemove();

            foreach (var graphElement in graph.elements)
            {
                if (graphElement is ConditionalExecution conditionalExecution)
                {
                    if (conditionalExecution.owner == owner) owner.ClearConditionalExecutionInexCache();
                }
            }
        }
    }
}


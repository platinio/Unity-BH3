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
    public abstract class ConditionalExecution : GameplayNode, IReportsProblems
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
        public virtual bool StopsItsOwnBranch => false;

        /// <summary>
        /// Whether this guard can take control away from a lower-priority sibling that is already running.
        /// False here: a doorman cannot open someone else's door.
        /// </summary>
        public virtual bool TakesOverLowerPriority => false;

        /// <summary>
        /// Whether this guard recomputes at all after entry. False for a doorman, which is asked once and
        /// never again.
        /// <para>
        /// Distinct from an empty <see cref="Triggers"/> list, which means the opposite: a watchman with no
        /// triggers recomputes <em>every tick</em>. "No schedule" and "a schedule that never says no" both
        /// present as an empty list, so the difference has to be stated rather than inferred.
        /// </para>
        /// </summary>
        public virtual bool HasRecomputeSchedule => false;

        /// <summary>
        /// When this guard is allowed to recompute. Empty for a doorman, which is evaluated at entry and
        /// never again, so it has no schedule to describe.
        /// <para>
        /// Declared here rather than left to a cast, for the same reason
        /// <see cref="StopsItsOwnBranch"/> is: everything that walks guards filters on capability, never on
        /// type. A reader that type-tested for <see cref="ReactiveGuard"/> would also have to be revisited
        /// by any future guard that carries a schedule without being one.
        /// </para>
        /// </summary>
        public virtual System.Collections.Generic.IReadOnlyList<GuardTrigger> Triggers =>
            System.Array.Empty<GuardTrigger>();

        public void UpdateOwner(BehaviorTreeNode owner)
        {
            this.owner = owner;
        }

        /// <summary>
        /// Where a guard's written-down schedule stops matching what its condition actually depends on
        /// (Unity-BH3#22).
        ///
        /// <para>
        /// Seeding writes a Function's declared keys into <see cref="GuardTrigger.Keys"/>, which is
        /// serialized, and <c>SeedMissingGuardTriggers</c> deliberately never revisits a trigger that already
        /// exists — rewriting a schedule an author chose would be worse than the every-tick default it fixes.
        /// The consequence is that the list freezes at the moment it was seeded, so a Function that later
        /// declares another key leaves the asset describing a guard that no longer exists.
        /// </para>
        ///
        /// <para>
        /// <b>This is not a runtime bug and is not reported as an error.</b> Inheritance unions the live
        /// declaration in at evaluation time, so the guard does wake on the new key. What is broken is the
        /// asset as a description: an author reads the trigger and concludes the opposite of what happens.
        /// </para>
        /// </summary>
        public virtual void CollectProblems(System.Collections.Generic.List<NodeProblem> into)
        {
            var declared = InheritedWatchedKeys.Resolve(this);

            var watchesKeys = false;

            foreach (var trigger in Triggers)
            {
                if (trigger == null || trigger.Kind != GuardTriggerKind.OnKeyChanged) continue;
                if (trigger.UsableKeyCount() > 0) watchesKeys = true;
            }

            // A guard whose condition is not connected reads the port's own default forever, so it depends on
            // nothing and every key it lists is provably stale. No false-positive risk here, unlike the
            // general "watches a key nothing declares" case below.
            if (watchesKeys && !AnyConditionConnected())
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Warning,
                    "Nothing is connected to this guard's condition, so it reads its own default and depends "
                    + "on nothing — but it still watches keys.",
                    "Connect a condition, or remove the key trigger."));
            }

            if (declared.Length == 0) return;

            foreach (var trigger in Triggers)
            {
                if (trigger == null || trigger.Kind != GuardTriggerKind.OnKeyChanged) continue;

                var missing = MissingFrom(trigger, declared);
                if (missing == null) continue;

                into.Add(new NodeProblem(NodeProblemSeverity.Warning,
                    $"This trigger does not list {missing}, which its condition declares. The guard does wake "
                    + "on it at runtime, so what the asset shows and what runs disagree.",
                    "Refresh Watched Keys."));
            }
        }

        /// <summary>
        /// Declared keys this trigger has not written down, as a readable list, or null when it has them all.
        /// <para>
        /// Only this direction is reported. The reverse — a key the trigger lists that nothing declares —
        /// cannot be distinguished from a deliberate hand-typed key, because seeded and hand-typed keys are
        /// byte-identical once written. Reporting it would fire on most existing content, which is why the
        /// unconnected-condition case above is handled separately: there it is provable.
        /// </para>
        /// </summary>
        private static string MissingFrom(GuardTrigger trigger, string[] declared)
        {
            System.Text.StringBuilder missing = null;

            foreach (var key in declared)
            {
                if (string.IsNullOrWhiteSpace(key) || trigger.Keys.Contains(key)) continue;

                if (missing == null) missing = new System.Text.StringBuilder();
                else missing.Append(", ");

                missing.Append('\'').Append(key).Append('\'');
            }

            return missing?.ToString();
        }

        private bool AnyConditionConnected()
        {
            foreach (var port in valueInputs)
            {
                if (port != null && port.hasValidConnection) return true;
            }

            return false;
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


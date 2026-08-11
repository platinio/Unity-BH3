using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// When a <see cref="ReactiveGuard"/> is allowed to recompute.
    ///
    /// <para>
    /// <b>A trigger does not cause evaluation. It marks the guard dirty.</b> Nothing outside the machine
    /// tick ever evaluates a guard: the tick asks, and the guard either recomputes or hands back what it
    /// last decided. That was chosen over evaluating on an independent timer, which would make whether a
    /// frame's decision saw a new value depend on Unity's component execution order — intermittent
    /// one-frame differences that reproduce on one machine and not another. It was also chosen over
    /// evaluating every due guard at the top of the tick, which is deterministic but pays for guards on
    /// branches the tree never considers.
    /// </para>
    ///
    /// <para>
    /// <b>Triggers are declared data, never ports.</b> A trigger's whole job is to be checkable
    /// <em>without</em> running the guard. A trigger fed by a graph would have to be executed every frame to
    /// find out whether the condition graph could be skipped, which costs exactly what it was meant to save.
    /// The condition stays an arbitrary port; the schedule is declared. That is the axis the whole design
    /// falls on: evaluated once may be arbitrary, evaluated repeatedly must be declared.
    /// </para>
    ///
    /// <para>
    /// A guard holds a list of these OR'd together, so a new kind is additive — a new subclass and nothing
    /// else. No change to the guard, the walks, the composites, or any existing asset.
    /// </para>
    /// </summary>
    public abstract class GuardTrigger
    {
        /// <summary>
        /// Whether this trigger claims the guard's answer may have moved since it last recomputed.
        /// </summary>
        /// <param name="owner">The node the guard is armed on, for reaching the agent.</param>
        /// <param name="secondsSinceEvaluated">Time since the last recompute, or a large value if never.</param>
        public abstract bool IsDue(BehaviorTreeNode owner, float secondsSinceEvaluated);

        /// <summary>Called after the guard recomputed, so a trigger can rearm.</summary>
        public virtual void OnEvaluated(BehaviorTreeNode owner) { }

        /// <summary>What the inspector, the dump and the cost display call this trigger.</summary>
        public abstract string Describe();
    }

    /// <summary>
    /// The honest escape hatch: recompute whenever asked. Identical to <c>EveryInterval(0)</c> and named
    /// separately so a guard that genuinely needs per-frame cost says so on the canvas.
    /// </summary>
    public sealed class EveryFrame : GuardTrigger
    {
        public override bool IsDue(BehaviorTreeNode owner, float secondsSinceEvaluated) => true;

        public override string Describe() => "every frame";
    }

    /// <summary>
    /// Recompute at a fixed rate, for continuous quantities that have no meaningful "changed" event —
    /// a distance, an angle, a resource level.
    ///
    /// <para>
    /// <paramref name="Deviation"/> exists because 200 agents sharing a 0.2s timer land on the same frame
    /// and produce a spike rather than a load. The phase is randomised per guard instance at construction,
    /// so agents spread out without anyone configuring it.
    /// </para>
    /// </summary>
    public sealed class EveryInterval : GuardTrigger
    {
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable]
        public float Seconds { get; set; } = 0.2f;

        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable]
        public float Deviation { get; set; } = 0.0f;

        /// <summary>
        /// The interval this instance is actually using, re-rolled after each evaluation. Not serialized:
        /// it is per-agent scheduling noise, and two agents sharing an asset must not share a phase.
        /// </summary>
        [Unity.VisualScripting.DoNotSerialize]
        private float currentInterval = -1.0f;

        public EveryInterval() { }

        public EveryInterval(float seconds, float deviation = 0.0f)
        {
            Seconds = seconds;
            Deviation = deviation;
        }

        public override bool IsDue(BehaviorTreeNode owner, float secondsSinceEvaluated)
        {
            if (currentInterval < 0.0f) Reroll();

            return secondsSinceEvaluated >= currentInterval;
        }

        public override void OnEvaluated(BehaviorTreeNode owner) => Reroll();

        private void Reroll()
        {
            currentInterval = Deviation <= 0.0f
                ? Seconds
                : Mathf.Max(0.0f, Seconds + Random.Range(-Deviation, Deviation));
        }

        public override string Describe() =>
            Deviation > 0.0f ? $"every {Seconds}s ±{Deviation}s" : $"every {Seconds}s";
    }

    /// <summary>
    /// Recompute when one of the agent facts this guard reads actually changes.
    ///
    /// <para>
    /// The default, and the cheapest: a guard whose keys have not moved costs one dictionary lookup per key
    /// instead of a graph run. Backed by <see cref="AgentVariableWriter"/>'s version counters — see there
    /// for why counters rather than subscriptions, and why the flight recorder could not have been the seam.
    /// </para>
    ///
    /// <para>
    /// <b>Agent-scope keys only.</b> Those are the facts a branch reacts to; a branch's own Graph-scope
    /// variables are per-call-site scratch, and a guard watching those would be watching its own noise.
    /// Anything else uses <see cref="EveryInterval"/>.
    /// </para>
    ///
    /// <para>
    /// The keys are auto-derived from the guard's graph and then editable, because derivation is right most
    /// of the time and silently wrong on a dynamically computed key. Showing the list is what makes that
    /// visible rather than mysterious.
    /// </para>
    /// </summary>
    public sealed class OnKeyChanged : GuardTrigger
    {
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable]
        public List<string> Keys { get; set; } = new();

        /// <summary>
        /// Whether a human edited <see cref="Keys"/>. Re-deriving must not silently discard a hand-tuned
        /// list, and a later reader should be able to tell a tuned list from a derived one.
        /// </summary>
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable]
        public bool HandEdited { get; set; }

        [Unity.VisualScripting.DoNotSerialize]
        private readonly Dictionary<string, int> seenVersions = new();

        public OnKeyChanged() { }

        public OnKeyChanged(params string[] keys)
        {
            Keys = new List<string>(keys);
        }

        public override bool IsDue(BehaviorTreeNode owner, float secondsSinceEvaluated)
        {
            // A guard with nothing to watch can never become dirty on its own. Reported by bt_verify rather
            // than quietly treated as every-frame, because a guard that never re-checks is a bug the author
            // should see, not a default worth guessing at.
            if (Keys == null || Keys.Count == 0) return false;

            var writer = WriterFor(owner);
            if (writer == null) return false;

            for (int i = 0; i < Keys.Count; i++)
            {
                string key = Keys[i];
                if (string.IsNullOrEmpty(key)) continue;

                int version = writer.VersionOf(key);

                if (!seenVersions.TryGetValue(key, out int seen) || seen != version) return true;
            }

            return false;
        }

        public override void OnEvaluated(BehaviorTreeNode owner)
        {
            if (Keys == null || Keys.Count == 0) return;

            var writer = WriterFor(owner);
            if (writer == null) return;

            for (int i = 0; i < Keys.Count; i++)
            {
                string key = Keys[i];
                if (string.IsNullOrEmpty(key)) continue;

                seenVersions[key] = writer.VersionOf(key);
            }
        }

        /// <summary>
        /// The agent's writer, or null when the guard is running outside a scene — which is the normal case
        /// in an edit-mode test and must not throw.
        /// </summary>
        private static AgentVariableWriter WriterFor(BehaviorTreeNode owner)
        {
            if (owner == null) return null;

            try
            {
                // A node resolves its GameObject through the machine, and an edit-mode test ticks a tree
                // with no machine at all. Nothing here may throw into the tree: a guard that cannot find an
                // agent simply has no versions to compare, which leaves it clean.
                var agent = owner.gameObject;

                return agent == null ? null : AgentVariableWriter.On(agent);
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public override string Describe() =>
            Keys == null || Keys.Count == 0 ? "on key changed (none)" : "on " + string.Join(", ", Keys) + " changed";
    }

    /// <summary>
    /// Recompute when something raises a named signal. A push with no value attached, for world events that
    /// are not agent state — a door opening, an alarm, a wave starting.
    ///
    /// <para>
    /// This is also the escape hatch for "I want logic deciding when to re-check": put the logic in whatever
    /// raises the signal, where it runs on its own terms rather than in the guard's hot path.
    /// </para>
    /// </summary>
    public sealed class OnSignal : GuardTrigger
    {
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable]
        public string Signal { get; set; }

        [Unity.VisualScripting.DoNotSerialize]
        private bool raised;

        public OnSignal() { }

        public OnSignal(string signal) => Signal = signal;

        /// <summary>Marks this trigger due. Called by <see cref="ReactiveGuard.RaiseSignal"/>.</summary>
        public void Raise() => raised = true;

        public override bool IsDue(BehaviorTreeNode owner, float secondsSinceEvaluated) => raised;

        public override void OnEvaluated(BehaviorTreeNode owner) => raised = false;

        public override string Describe() => $"on signal '{Signal}'";
    }
}

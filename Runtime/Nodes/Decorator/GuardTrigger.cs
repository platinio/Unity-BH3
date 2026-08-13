using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>Which question a <see cref="GuardTrigger"/> asks.</summary>
    public enum GuardTriggerKind
    {
        /// <summary>An agent fact this guard reads has changed value. The default, and the cheapest.</summary>
        OnKeyChanged,

        /// <summary>A fixed amount of time has passed, for continuous quantities with no "changed" event.</summary>
        EveryInterval,

        /// <summary>Always due. The honest escape hatch.</summary>
        EveryFrame,
    }

    /// <summary>
    /// When a <see cref="ReactiveGuard"/> is allowed to recompute.
    ///
    /// <para>
    /// <b>A trigger does not cause evaluation. It marks the guard dirty.</b> Nothing outside the machine tick
    /// ever evaluates a guard: the tick asks, and the guard either recomputes or hands back what it last
    /// decided. That was chosen over evaluating on an independent timer, which would make whether a frame's
    /// decision saw a new value depend on Unity's component execution order — intermittent one-frame
    /// differences that reproduce on one machine and not another. It was also chosen over evaluating every
    /// due guard at the top of the tick, which is deterministic but pays for guards on branches the tree
    /// never considers.
    /// </para>
    ///
    /// <para>
    /// <b>Triggers are declared data, never ports.</b> A trigger's whole job is to be checkable
    /// <em>without</em> running the guard. A trigger fed by a graph would have to be executed every frame to
    /// find out whether the condition graph could be skipped, which costs exactly what it was meant to save.
    /// The condition stays an arbitrary port; the schedule is declared.
    /// </para>
    ///
    /// <para>
    /// <b>One concrete type with a kind, rather than a subclass per kind.</b> The subclass version was
    /// tidier to extend and could not be edited: Visual Scripting's reflected inspector has no type picker
    /// for an abstract element type, so the trigger list rendered as nothing a designer could add to. A
    /// schedule nobody can see or change is not a feature. The kinds are mutually exclusive variants of one
    /// decision — a trigger is exactly one of them — which is what makes an enum honest here and dishonest
    /// for a capability like <see cref="ReactiveGuard.StopsItsOwnBranch"/>, where the combinations are the point.
    /// Adding a kind is an enum value plus a branch in <see cref="IsDue"/>, and existing assets are
    /// unaffected.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <c>[Inspectable]</c> on the class, and <c>[Serializable]</c> for anything Unity-side that reflects
    /// over it. Neither draws the type on its own — that needs a registered inspector, which is what
    /// <c>GuardTriggerInspector</c> in the editor assembly is for.
    /// </remarks>
    [System.Serializable]
    [Inspectable]
    public sealed class GuardTrigger
    {
        [Serialize, Inspectable, InspectorLabel("When")]
        public GuardTriggerKind Kind { get; set; } = GuardTriggerKind.OnKeyChanged;

        /// <summary>
        /// The agent facts this guard reads.
        /// <para>
        /// Authored by hand today, and seeded by <c>GuardOnVariable</c> in the one case where the key is
        /// known without inspecting anything. Deriving them from the guard's graph is not built — see the
        /// spec's <i>Cut: derived keys</i> note. Until it is, a wrong key here is silent: the guard simply
        /// never wakes.
        /// </para>
        /// </summary>
        [Serialize, Inspectable, InspectorLabel("Keys")]
        public List<string> Keys { get; set; } = new();

        [Serialize, Inspectable, InspectorLabel("Seconds")]
        public float Seconds { get; set; } = 0.2f;

        /// <summary>
        /// Randomises the interval per guard. 200 agents sharing a 0.2s timer land on the same frame and
        /// produce a spike rather than a load, so the phase is spread without anyone configuring it.
        /// </summary>
        [Serialize, Inspectable, InspectorLabel("Deviation")]
        public float Deviation { get; set; }

        [DoNotSerialize] private readonly Dictionary<string, int> seenVersions = new();
        [DoNotSerialize] private float currentInterval = -1.0f;

        // Resolving the writer is a component lookup, and this trigger is asked twice per guard evaluation
        // on the path the whole feature exists to keep cheap. Cached against the agent it was resolved for,
        // so a pooled node reused on a different agent still re-resolves.
        [DoNotSerialize] private GameObject cachedAgent;
        [DoNotSerialize] private AgentVariableWriter cachedWriter;

        public GuardTrigger() { }

        /// <summary>
        /// Watches the named agent facts. Blank names are dropped rather than stored: a blank is skipped at
        /// evaluation, so keeping one would leave a non-empty list that can never mark the guard dirty --
        /// and would hide it from the bt_verify check for a guard that watches nothing.
        /// </summary>
        public static GuardTrigger KeyChanged(params string[] keys)
        {
            var usable = new List<string>();

            if (keys != null)
            {
                foreach (var key in keys)
                {
                    if (!string.IsNullOrWhiteSpace(key)) usable.Add(key);
                }
            }

            return new GuardTrigger { Kind = GuardTriggerKind.OnKeyChanged, Keys = usable };
        }

        /// <summary>How many of <see cref="Keys"/> could actually wake this guard. Blanks do not count.</summary>
        public int UsableKeyCount()
        {
            if (Keys == null) return 0;

            int count = 0;

            foreach (var key in Keys)
            {
                if (!string.IsNullOrWhiteSpace(key)) count++;
            }

            return count;
        }

        public static GuardTrigger Interval(float seconds, float deviation = 0.0f) =>
            new() { Kind = GuardTriggerKind.EveryInterval, Seconds = seconds, Deviation = deviation };

        public static GuardTrigger EveryFrame() => new() { Kind = GuardTriggerKind.EveryFrame };

        /// <summary>Whether this trigger claims the guard's answer may have moved since it last recomputed.</summary>
        public bool IsDue(BehaviorTreeNode owner, float secondsSinceEvaluated)
        {
            switch (Kind)
            {
                case GuardTriggerKind.EveryFrame:
                    return true;

                case GuardTriggerKind.EveryInterval:
                    if (currentInterval < 0.0f) Reroll();
                    return secondsSinceEvaluated >= currentInterval;

                case GuardTriggerKind.OnKeyChanged:
                    return AnyKeyMoved(owner);

                default:
                    return true;
            }
        }

        /// <summary>Called after the guard recomputed, so the trigger can rearm.</summary>
        public void OnEvaluated(BehaviorTreeNode owner)
        {
            switch (Kind)
            {
                case GuardTriggerKind.EveryInterval:
                    Reroll();
                    break;

                case GuardTriggerKind.OnKeyChanged:
                    RememberVersions(owner);
                    break;
            }
        }

        /// <summary>
        /// A key counts as moved when its version differs from the one seen at the last evaluation. Agent
        /// scope only: those are the facts branches react to, and a branch's own graph variables are
        /// per-call-site scratch a guard would only be watching itself write.
        /// </summary>
        private bool AnyKeyMoved(BehaviorTreeNode owner)
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

                if (!seenVersions.TryGetValue(key, out int seen) || seen != writer.VersionOf(key)) return true;
            }

            return false;
        }

        private void RememberVersions(BehaviorTreeNode owner)
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

        private void Reroll()
        {
            currentInterval = Deviation <= 0.0f
                ? Seconds
                : Mathf.Max(0.0f, Seconds + Random.Range(-Deviation, Deviation));
        }

        /// <summary>
        /// The writer on the agent this guard belongs to, or null when there is none — which includes a
        /// guard ticked outside a scene, the normal case in an edit-mode test. Nothing here may throw into
        /// the tree.
        /// <para>
        /// <see cref="AgentVariableWriter.Find"/> rather than <c>On</c>: reading versions must never add a
        /// component to the agent. Nothing has written yet in that case, so every version is zero and the
        /// guard is correctly clean; the component appears as soon as anything publishes a fact, because
        /// every write path is get-or-add.
        /// </para>
        /// </summary>
        private AgentVariableWriter WriterFor(BehaviorTreeNode owner)
        {
            if (owner == null) return null;

            try
            {
                var agent = owner.gameObject;

                if (agent == null) return null;
                if (ReferenceEquals(agent, cachedAgent)) return cachedWriter;

                cachedAgent = agent;
                cachedWriter = AgentVariableWriter.Find(agent);

                return cachedWriter;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>What the inspector, the dump and the cost display call this trigger.</summary>
        public string Describe()
        {
            switch (Kind)
            {
                case GuardTriggerKind.EveryFrame:
                    return "every frame";

                case GuardTriggerKind.EveryInterval:
                    return Deviation > 0.0f ? $"every {Seconds}s ±{Deviation}s" : $"every {Seconds}s";

                case GuardTriggerKind.OnKeyChanged:
                    return Keys == null || Keys.Count == 0
                        ? "on key changed (none)"
                        : "on " + string.Join(", ", Keys) + " changed";

                default:
                    return Kind.ToString();
            }
        }
    }
}

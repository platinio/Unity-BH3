using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The agent's fact store: writes agent variables, records who did it, and counts what changed.
    ///
    /// <para>
    /// <b>Two jobs.</b> It began as the attribution seam for sensors — components outside the graph, which
    /// have no node guid for a recording to point at. It is now also the <b>version registry</b> reactive
    /// guards compare against, which is why nodes and Visual Scripting units write through it too even
    /// though they do their own recording. If those two ever need to come apart, the counters are the half
    /// to move.
    /// </para>
    ///
    /// <para>
    /// The values this writes — <c>hasTarget</c>, <c>lastKnownTargetPos</c>, <c>alertLevel</c> — are knowledge
    /// about the world rather than a behavior's output. A branch is conditional by construction, so anything
    /// only a branch produces is unreliable by definition; knowledge has to come from something that runs
    /// unconditionally, which is what a sensor is. That is also why a branch may never depend on a sibling
    /// branch having run.
    /// </para>
    ///
    /// <para>
    /// It exists rather than each sensor calling <c>Variables</c> directly because of the why-inspector. The
    /// question that tool is built to answer — "who changed the value that flipped this guard?" — has no
    /// answer for a sensor unless the writer says so: a sensor is outside the graph and has no node guid to
    /// record. Every write made through here reports itself, so the sensor shows up by name in an explanation
    /// and in the variable watch without doing anything else.
    /// </para>
    ///
    /// <para>
    /// <b>A component, not a base class.</b> A sensor usually already derives from something — a perception
    /// component, a pooled behaviour, an interface-driven base — and a debugging concern has no business
    /// spending that one inheritance slot. Add this to the agent and call it, or reach it with
    /// <see cref="On"/>; several components can share one and each is attributed separately, because the
    /// caller names itself on every write rather than the writer naming itself once.
    /// </para>
    ///
    /// <para>
    /// A deliberately small seam, not a fact system: no registry and no <c>[ProvidesFact]</c> attribute, since
    /// which facts exist and how they are validated against a prefab is a separate design. This is the part
    /// that had to exist for an explanation to name a sensor at all, shaped so that design can be built on top
    /// of it rather than around it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Variables))]
    [AddComponentMenu("ArcaneOnyx/BH3/Agent Variable Writer")]
    public sealed class AgentVariableWriter : MonoBehaviour
    {
        private BehaviorTreeMachine machine;
        private Variables variables;
        private bool bound;

        /// <summary>
        /// How many times each key has actually changed. The seam <c>OnKeyChanged</c> is built on.
        ///
        /// <para>
        /// A reactive guard needs to know that a fact it depends on has moved, and Unity gives it nothing to
        /// listen to: <c>VariableDeclarations.OnVariableChanged</c> is internal and carries no name, no old
        /// value and no new one. The only other observer in the project is the flight recorder, and that is
        /// <c>[Conditional]</c>-gated — the compiler deletes those call sites in a shipped build, so a guard
        /// built on it would be event-driven in the editor and permanently clean in a player build. Working
        /// in the editor and freezing in the build is the worst failure this could have.
        /// </para>
        ///
        /// <para>
        /// Counters rather than subscriptions, deliberately. A guard caches the version of each key it reads
        /// and compares; nothing registers, so nothing has to unregister when a branch is aborted, no
        /// listener outlives the agent, and arming a guard twice cannot leave a live duplicate. The cost of
        /// asking is a dictionary lookup per key — one or two in practice — against running a flow graph.
        /// </para>
        ///
        /// <para>
        /// Not <c>[Conditional]</c>, and not gated on anything: this one ships.
        /// </para>
        /// </summary>
        private readonly System.Collections.Generic.Dictionary<string, int> versions = new();

        /// <summary>
        /// How many times <paramref name="key"/> has changed on this agent. Zero for a key nothing has
        /// written, which is also the value a guard caches before its first evaluation — so a key that never
        /// moves never makes a guard look dirty.
        /// </summary>
        public int VersionOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;

            return versions.TryGetValue(key, out int version) ? version : 0;
        }

        /// <summary>
        /// Writes an agent variable and records that it moved, in one call.
        ///
        /// <para>
        /// <b>This is the only way BH3 writes agent scope.</b> The version bump cannot ride on
        /// <c>VariableDeclarations.Set</c> — that is Unity's type, and its change event is internal and
        /// carries no name — so it has to happen wherever the set happens. Three call sites each doing
        /// <c>Set</c> then remembering to bump is a rule the fourth writer will break, and the failure is
        /// silent: guards watching that key simply stop waking. Putting both halves behind one method is
        /// what makes forgetting impossible.
        /// </para>
        ///
        /// <para>
        /// <b>The version counts changes, not writes.</b> A tree node that writes <c>hasTarget = true</c>
        /// every frame must not make every guard watching it recompute every frame — that turns the cheapest
        /// trigger into the most expensive one, which is the whole thing the counter exists to avoid. The
        /// write still happens either way; only the bump is conditional.
        /// </para>
        /// <para>
        /// Comparison is by <see cref="object.Equals(object, object)"/>, so an object <em>mutated in place</em>
        /// compares equal to itself and does not bump. Watch a scalar fact rather than a container.
        /// </para>
        /// </summary>
        /// <returns>Whether the value actually changed.</returns>
        public bool SetAgentVariable(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return false;

            Bind();

            if (variables == null) return false;

            var declarations = variables.declarations;
            bool changed = !declarations.IsDefined(key) || !Equals(declarations.Get(key), value);

            declarations.Set(key, value);

            if (changed) Bump(key);

            return changed;
        }

        /// <summary>Records that a key changed. Private, so the bump cannot be issued without the write.</summary>
        private void Bump(string key)
        {
            versions[key] = VersionOf(key) + 1;
        }

        /// <summary>
        /// The writer on this GameObject, adding one if it is missing.
        ///
        /// <para>
        /// Get-or-add rather than a hard requirement so a sensor works on an agent prefab that predates it,
        /// and so a scene does not have to be edited to keep recording. <c>RequireComponent</c> on the sensor
        /// covers the case where the designer adds the sensor in the editor; this covers everything else.
        /// </para>
        /// </summary>
        public static AgentVariableWriter On(GameObject agent)
        {
            if (agent == null) return null;

            return agent.TryGetComponent<AgentVariableWriter>(out var existing)
                ? existing
                : agent.AddComponent<AgentVariableWriter>();
        }

        /// <summary>
        /// Writes an agent variable on <paramref name="target"/>, versioning it only when something could be
        /// watching.
        ///
        /// <para>
        /// A version is only ever <em>read</em> on a GameObject that runs a behavior tree, because a reactive
        /// guard is a node inside one. Recording a version anywhere else is storage nobody will query — and
        /// since <see cref="On"/> is get-or-add, doing it would attach this component, and a
        /// <c>Variables</c> with it, to whatever the write happened to target. A tree writing a flag on a
        /// door has no business changing the door's component list.
        /// </para>
        ///
        /// <para>
        /// The test is "has a machine right now", so a write that lands before a machine is added at runtime
        /// is not versioned. Components authored in a scene or prefab are found regardless of Awake order, so
        /// that only matters if machines are attached dynamically after facts are already being published.
        /// </para>
        /// </summary>
        /// <returns>Whether the value changed <em>and</em> the change was versioned.</returns>
        public static bool SetOn(GameObject target, string key, object value)
        {
            if (target == null || string.IsNullOrEmpty(key)) return false;

            if (!target.TryGetComponent<BehaviorTreeMachine>(out _))
            {
                Variables.Object(target).Set(key, value);
                return false;
            }

            return On(target).SetAgentVariable(key, value);
        }

        /// <summary>
        /// The writer already on this GameObject, or null. Unlike <see cref="On"/> this never adds one, so it
        /// is safe on the guard evaluation path — a guard asking about versions must not mutate the agent,
        /// and before anything has written there is nothing to compare against anyway.
        /// </summary>
        public static AgentVariableWriter Find(GameObject agent)
        {
            if (agent == null) return null;

            return agent.TryGetComponent<AgentVariableWriter>(out var existing) ? existing : null;
        }

        /// <summary>
        /// Writes a variable and attributes it to <paramref name="source"/> by type name — which is what a
        /// designer sees in the hierarchy, and is one fewer string to keep in sync than naming it by hand.
        /// </summary>
        /// <returns>Whether the value actually changed. False means the write was dropped as a duplicate.</returns>
        public bool Write(Component source, string key, object value)
        {
            return Write(source != null ? source.GetType().Name : null, key, value);
        }

        /// <summary>
        /// Writes a variable, attributed to a name of the caller's choosing. Use this when the writer is not a
        /// component, or when the type name is not what a reader should see.
        /// </summary>
        /// <returns>Whether the value actually changed. False means the write was dropped as a duplicate.</returns>
        public bool Write(string sourceName, string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return false;

            Bind();

            if (variables == null) return false;

            var declarations = variables.declarations;
            var previous = declarations.IsDefined(key) ? declarations.Get(key) : null;

            // Unchanged values are dropped rather than written. A fact recomputed every frame would otherwise
            // fill the recording with "still false" and push the events that matter out of the buffer — the
            // same reason guards are recorded on transition only.
            if (Equals(previous, value)) return false;

            // Recorded before the write, while the previous value still exists — "what did it change from" is
            // half of what makes a write worth recording. The call compiles out entirely outside the editor
            // and dev builds, arguments included.
            Debugging.BehaviorTreeRecorder.ExternalVariableWrite(
                machine, string.IsNullOrEmpty(sourceName) ? "(external)" : sourceName, key, previous, value);

            // Reached only on a real change, which the early-out above established -- so the compare inside
            // SetAgentVariable agrees and the version moves.
            SetAgentVariable(key, value);

            return true;
        }

        /// <summary>The current value of a variable, or null when nothing has written it yet.</summary>
        public object Read(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            Bind();

            if (variables == null) return null;

            return variables.declarations.IsDefined(key) ? variables.declarations.Get(key) : null;
        }

        private void Awake() => Bind();

        /// <summary>
        /// Resolves the machine and the variable store, lazily.
        ///
        /// <para>
        /// Lazy rather than Awake-only because sensors commonly publish their first values from their own
        /// <c>Awake</c> — deliberately, so a guard never reads a variable nothing has declared — and component
        /// Awake order is not something a sensor should have to win.
        /// </para>
        /// </summary>
        private void Bind()
        {
            if (bound) return;

            machine = GetComponent<BehaviorTreeMachine>();
            variables = GetComponent<Variables>();
            bound = true;
        }
    }
}

using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Writes agent variables on behalf of components that are not nodes, and records who did it.
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

            declarations.Set(key, value);

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

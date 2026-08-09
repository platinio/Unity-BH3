using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Base for a sensor that publishes agent facts, with write attribution for free.
    ///
    /// <para>
    /// Facts — <c>lastKnownTargetPos</c>, <c>hasTarget</c>, <c>alertLevel</c> — are knowledge about the world,
    /// not a behavior's output. A branch is conditional by construction, so anything only a branch produces is
    /// unreliable by definition; knowledge has to come from something that runs unconditionally. That is what
    /// a sensor is, and it is why a branch may never depend on a sibling branch having run.
    /// </para>
    ///
    /// <para>
    /// The reason this type exists rather than each sensor calling <c>Variables</c> directly is the
    /// why-inspector. The question it is built to answer — "who changed the value that flipped this guard?" —
    /// has no answer for facts unless the writer says so, because a sensor is outside the graph and has no
    /// node guid to record. <see cref="PublishFact"/> reports every change it makes, so a sensor written
    /// against this class shows up by name in an explanation without doing anything else.
    /// </para>
    ///
    /// <para>
    /// A deliberately small seam, not the fact system: there is no registry and no
    /// <c>[ProvidesFact]</c> attribute here, because which facts exist and how they are validated against a
    /// prefab is a separate design. This is the part that had to exist for an explanation to name a sensor at
    /// all, and it is shaped so that design can be built on top of it rather than around it.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Variables))]
    public abstract class AgentFactPublisher : MonoBehaviour
    {
        private BehaviorTreeMachine machine;
        private Variables variables;

        /// <summary>
        /// What an explanation calls this sensor. The GameObject's component name by default, which is what a
        /// designer would look for in the hierarchy.
        /// </summary>
        public virtual string WriterName => GetType().Name;

        protected virtual void Awake()
        {
            machine = GetComponent<BehaviorTreeMachine>();
            variables = GetComponent<Variables>();
        }

        /// <summary>
        /// Publishes a fact onto the agent's own variables, and records the change against this sensor's name.
        ///
        /// <para>
        /// Unchanged values are dropped rather than written. A fact recomputed every frame would otherwise
        /// fill the recording with "still false" and push the events that matter out of the buffer — the same
        /// reason guards are recorded on transition only.
        /// </para>
        /// </summary>
        protected void PublishFact(string key, object value)
        {
            if (string.IsNullOrEmpty(key) || variables == null) return;

            var declarations = variables.declarations;
            var previous = declarations.IsDefined(key) ? declarations.Get(key) : null;

            if (Equals(previous, value)) return;

            // Recorded before the write, while the previous value still exists — "what did it change from" is
            // half of what makes a write worth recording.
            Debugging.BehaviorTreeRecorder.ExternalVariableWrite(machine, WriterName, key, previous, value);

            declarations.Set(key, value);
        }

        /// <summary>The current value of a fact, or null when nothing has published it yet.</summary>
        protected object ReadFact(string key)
        {
            if (string.IsNullOrEmpty(key) || variables == null) return null;

            return variables.declarations.IsDefined(key) ? variables.declarations.Get(key) : null;
        }
    }
}

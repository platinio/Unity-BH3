using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Which agent the debugging panels are talking about, and where that answer came from.
    ///
    /// <para>
    /// Deliberately not a picker. The canvas is already showing one particular agent's tree instance, so a
    /// control choosing a different agent is not merely an odd second control — it is a way to get a wrong
    /// answer. Clicking a node would explain <em>another</em> agent's history for that guid, and the call site
    /// path would even read correctly, because the asset is the same, while every tick belonged to someone
    /// else.
    /// </para>
    ///
    /// <para>
    /// Shared rather than duplicated per panel, which matters more here than it looks. The why-inspector and
    /// the timeline render into the same canvas: if they resolved the agent separately and ever disagreed, the
    /// scrubber would ghost the canvas with one agent's history while the explanation described another's —
    /// the same wrong answer, arrived at by a different route.
    /// </para>
    /// </summary>
    public static class BehaviorTreeDebugTarget
    {
        /// <summary>
        /// The agent to explain or scrub, or null when nothing says which. <paramref name="source"/> names how
        /// it was decided, so a panel can show it and a reader can tell "this is the agent on screen" from
        /// "this is the only one running".
        /// </summary>
        public static BehaviorTreeMachine Resolve(GraphCore.IGraphContext context, out string source)
        {
            // The canvas's own reference knows which machine it was opened through. This is the answer whenever
            // the tree is being watched live, which is the case the panels exist for.
            if (context?.reference != null)
            {
                if (context.reference.machine is BehaviorTreeMachine viewed && viewed.FlightRecorder != null)
                {
                    source = "shown on this canvas";
                    return viewed;
                }

                var owner = context.reference.gameObject;
                if (owner != null)
                {
                    var onOwner = owner.GetComponent<BehaviorTreeMachine>();
                    if (onOwner?.FlightRecorder != null)
                    {
                        source = "shown on this canvas";
                        return onOwner;
                    }
                }
            }

            // The tree was opened as a bare asset, so the canvas has no agent. Hierarchy selection is then the
            // only statement of intent the user has made.
            var selected = Selection.activeGameObject;
            if (selected != null)
            {
                var onSelection = selected.GetComponent<BehaviorTreeMachine>();
                if (onSelection?.FlightRecorder != null)
                {
                    source = "selected in the hierarchy";
                    return onSelection;
                }
            }

            // Nothing said which agent, but there is only one it could be. Picking it cannot be wrong, and
            // refusing to would make the panels useless in the common single-enemy case.
            var only = SingleRecordingMachine();
            if (only != null)
            {
                source = "the only agent recording";
                return only;
            }

            source = null;
            return null;
        }

        /// <summary>
        /// The one machine recording, or null when there are none or several. Walks the scene only when the
        /// cheaper answers failed, which is why the registry is not consulted — it holds recordings rather than
        /// agents, and a topology needs the machine.
        /// </summary>
        public static BehaviorTreeMachine SingleRecordingMachine()
        {
            if (!Application.isPlaying) return null;

            BehaviorTreeMachine found = null;

            foreach (var machine in Object.FindObjectsByType<BehaviorTreeMachine>(FindObjectsSortMode.None))
            {
                if (machine == null || machine.FlightRecorder == null) continue;

                if (found != null) return null;

                found = machine;
            }

            return found;
        }
    }
}

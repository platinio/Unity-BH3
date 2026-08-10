using System;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// What a breakpoint hit actually does to the editor: pause, say so, and put every panel on the moment it
    /// stopped.
    ///
    /// <para>
    /// Separate from <see cref="BehaviorTreeBreakpoints"/> so the runtime store stays free of editor types and
    /// testable without a scene. The store decides <em>whether</em> something fired; this decides what that
    /// means, and it is the only part of the component that could not run in a player build.
    /// </para>
    ///
    /// <para>
    /// <b>Pausing is not enough on its own.</b> The editor stopping mid-frame tells you that something
    /// happened and nothing about what, and the panels would still be parked wherever they were — which is a
    /// worse failure here than elsewhere, because the ghosted canvas would be showing a tick that is not the
    /// one you stopped at. So a hit also selects the node, moves the playhead to the hit tick, and marks the
    /// node on the canvas. The why-inspector and the variable watch both follow the playhead already, so
    /// moving it is what makes all three answer for the same moment.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class BehaviorTreeBreakpointResponder
    {
        /// <summary>
        /// Whether a hit has already taken the editor down this frame.
        ///
        /// <para>
        /// <see cref="Debug.Break"/> pauses at the end of the frame, not at the call, so the rest of the tick
        /// still runs and more breakpoints can fire behind this one. Without this the console gets a burst and,
        /// worse, the last one to fire decides what is selected and where the playhead sits — so the editor
        /// would stop on breakpoint A and show you breakpoint D.
        /// </para>
        /// </summary>
        private static bool broken;

        /// <summary>
        /// How often the agent filter is re-resolved. Resolution can walk the scene in its last fallback, so
        /// it does not belong on every editor frame; a quarter of a second is far faster than anyone can
        /// change what they are looking at.
        /// </summary>
        private const double ResolveInterval = 0.25;

        private static double nextResolve;

        static BehaviorTreeBreakpointResponder()
        {
            BehaviorTreeBreakpoints.Hit -= OnHit;
            BehaviorTreeBreakpoints.Hit += OnHit;

            EditorApplication.update -= TrackDebugTarget;
            EditorApplication.update += TrackDebugTarget;

            EditorApplication.pauseStateChanged += state =>
            {
                if (state == PauseState.Unpaused) Resume();
            };

            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode) Resume();
            };
        }

        /// <summary>
        /// The hit the editor is currently stopped on, or null. Read by the canvas to mark the node and by the
        /// breakpoints panel to say what happened.
        /// </summary>
        public static BehaviorTreeBreakpointHit? Current { get; private set; }

        /// <summary>Whether this node is the one the editor stopped on, for the canvas highlight.</summary>
        public static bool IsStoppedOn(BehaviorTreeNode node)
        {
            return node != null && Current.HasValue && Current.Value.SubjectGuid == node.guid;
        }

        /// <summary>The agent breakpoints are currently narrowed to, or null when they fire for any.</summary>
        public static BehaviorTreeMachine FilteredTo { get; private set; }

        /// <summary>
        /// Keeps <see cref="BehaviorTreeBreakpoints.AgentFilter"/> pointed at whichever agent the debugging
        /// panels are about.
        ///
        /// <para>
        /// Pushed from here rather than pulled by the panels, because a breakpoint fires while the tree ticks
        /// and nothing guarantees a panel was drawn that frame — a filter that only updated on repaint would
        /// be stale exactly when it is consulted. It is deliberately not a control: the agent comes from
        /// <see cref="BehaviorTreeDebugTarget"/>, the same resolution the canvas ghosting, the why-inspector
        /// and the variable watch all use, so the editor cannot stop for one agent while describing another.
        /// </para>
        /// </summary>
        private static void TrackDebugTarget()
        {
            if (EditorApplication.timeSinceStartup < nextResolve) return;

            nextResolve = EditorApplication.timeSinceStartup + ResolveInterval;

            var machine = BehaviorTreeDebugTarget.Resolve(GraphWindow.activeContext, out _);

            FilteredTo = machine;
            BehaviorTreeBreakpoints.AgentFilter = machine != null ? machine.FlightRecorder : null;
        }

        private static void OnHit(BehaviorTreeBreakpointHit hit)
        {
            // Nothing here means anything outside play mode, and an edit-mode test driving the recorder
            // directly should not be pausing an editor or writing to its console.
            if (!EditorApplication.isPlaying || broken) return;

            broken = true;
            Current = hit;

            Debug.Log(hit.Describe());
            Debug.Break();

            // Deferred rather than done here. This runs inside the recorder, inside the tree's tick: selecting
            // canvas elements and repainting windows from there would reach into the editor's UI in the middle
            // of a frame the tree is still using. delayCall lands after the pause has taken effect, which is
            // also when the panels can actually show the moment.
            EditorApplication.delayCall += () => Reveal(hit);
        }

        /// <summary>
        /// Puts the canvas and the panels on the moment that stopped the editor.
        ///
        /// <para>
        /// The scrub is what does the real work. Both the why-inspector and the variable watch answer for the
        /// playhead rather than for the present, so moving it once makes the explanation, the table and the
        /// ghosted canvas describe the same tick — the rule Component 2 established and the reason a hit does
        /// not need to notify each panel individually.
        /// </para>
        /// </summary>
        private static void Reveal(BehaviorTreeBreakpointHit hit)
        {
            if (!Current.HasValue) return;

            Select(hit.SubjectGuid);

            // Only when the timeline is showing the recording this tick came from. A tick number means nothing
            // outside its own recording — the rule BehaviorTreeDebugSession.TickFor exists to enforce — so with
            // an exported recording open while a live agent hits a breakpoint, moving that file's playhead to
            // tick 412 would park it confidently on a moment that never happened in it. A null session is the
            // ordinary case of the timeline never having been opened, where RequestScrub no-ops anyway.
            var showing = BehaviorTreeDebugSession.Recording;

            if (showing == null || ReferenceEquals(showing, hit.Recording))
            {
                BehaviorTreeTimelinePanel.RequestScrub(hit.Tick);
            }

            foreach (var window in GraphWindow.tabs)
            {
                window.Repaint();
            }
        }

        /// <summary>
        /// Selects the node on whichever open canvas has it.
        ///
        /// <para>
        /// By guid across every tab rather than on the active one, because the tree that hit the breakpoint is
        /// often not the tree in front of you — that is most of the point of arming one. A guid that no open
        /// canvas holds simply selects nothing: the pause and the log line still happened, and the breakpoints
        /// panel can still say what fired.
        /// </para>
        /// </summary>
        private static void Select(Guid guid)
        {
            if (guid == Guid.Empty) return;

            foreach (var window in GraphWindow.tabs)
            {
                if (window?.context?.graph is not BehaviorTreeGraph graph) continue;

                foreach (var node in graph.Nodes)
                {
                    if (node == null || node.guid != guid || !node.IsVisible) continue;

                    window.context.selection.Select(node);
                    return;
                }
            }
        }

        /// <summary>
        /// Hands the editor back. Clearing <see cref="Current"/> is what takes the highlight off the canvas —
        /// a mark left behind after you pressed play would claim the tree is stopped there when it is running.
        /// </summary>
        private static void Resume()
        {
            broken = false;
            Current = null;
        }
    }
}

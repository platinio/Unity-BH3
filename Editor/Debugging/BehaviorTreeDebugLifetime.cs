using UnityEditor;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Hands the canvas and the panels back to the present whenever play mode starts or stops.
    ///
    /// <para>
    /// <see cref="BehaviorTreeScrubOverride"/> and <see cref="BehaviorTreeDebugSession"/> are static, and
    /// until this existed the only thing that cleared either of them was the timeline panel's own
    /// <c>OnGUI</c> -- which runs only while the bottom dock is expanded and its tab is the visible one, and
    /// the dock's collapsed flag is serialized state one click away. Freezing while collapsed is right
    /// <i>within</i> a session, and <see cref="BehaviorTreeDebugSession"/> says so: the canvas stays ghosted
    /// at the same tick, so the watch should too. It stops being right at a play-mode boundary, because the
    /// thing being frozen no longer exists.
    /// </para>
    ///
    /// <para>
    /// Scrub, collapse the dock, stop play, and the <b>edit-mode</b> canvas went on drawing a dead run's
    /// statuses at ghost alpha with no banner anywhere -- the only "SCRUBBING" banner lives inside the panel
    /// that is collapsed. The session went on handing the variable watch a dead recorder, which drew it as
    /// live and pinned its ring in memory. With domain reload disabled both survived into the next play
    /// session, so a new run opened wearing the old run's ghost.
    /// </para>
    ///
    /// <para>
    /// The rule is deliberately blunt: <b>a play-mode boundary resets what the debugger is looking at</b>, in
    /// both directions and with no exceptions for what kind of recording it was. Nothing is lost by it. The
    /// timeline holds a loaded file in its own field and republishes it the next time it draws, so a file
    /// survives; and when the dock is collapsed nothing is being read anyway. A conditional rule -- clear
    /// only live recorders, keep loaded ones -- would restore exactly the state this exists to prevent: the
    /// debugger still looking at something after the moment it was looking at has gone.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class BehaviorTreeDebugLifetime
    {
        static BehaviorTreeDebugLifetime()
        {
            // Paired rather than a lambda so a domain reload that runs this twice cannot subscribe twice.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Public so a test can drive the transition without entering play mode, which an EditMode test
        /// cannot do.
        /// </summary>
        public static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                case PlayModeStateChange.ExitingPlayMode:
                    Reset();
                    break;
            }
        }

        /// <summary>
        /// Clears the debugging statics together. They describe one thing between them -- which agent, which
        /// recording, at which tick -- so clearing one without the others leaves the canvas and the panels
        /// describing different moments, which is the failure <see cref="BehaviorTreeDebugSession"/> exists
        /// to prevent.
        ///
        /// <para>
        /// <see cref="BehaviorTreeDebugTarget.Forget"/> is the third because the target memo is a static
        /// that outlives a play-mode boundary too. Its own clock is a quarter of a second, so it is not
        /// finding 1.2 by itself, but a boundary is exactly the moment its answer stops being one -- the
        /// agent it names is about to be destroyed or replaced.
        /// </para>
        /// </summary>
        public static void Reset()
        {
            BehaviorTreeScrubOverride.Clear();
            BehaviorTreeDebugSession.Clear();
            BehaviorTreeDebugTarget.Forget();
        }
    }
}

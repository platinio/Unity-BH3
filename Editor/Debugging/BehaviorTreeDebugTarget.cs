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
        /// How long a resolution that had to walk the scene may be reused. Matches the cadence
        /// <see cref="BehaviorTreeBreakpointResponder"/> already runs its own scene work at.
        /// </summary>
        private const double MemoSeconds = 0.25;

        private static GraphCore.IGraphContext memoContext;
        private static GameObject memoSelection;
        private static Remembered<BehaviorTreeMachine> memoMachine;
        private static string memoSource;
        private static double memoAt = double.NegativeInfinity;
        private static bool memoValid;

        /// <summary>
        /// The agent to explain or scrub, or null when nothing says which. <paramref name="source"/> names how
        /// it was decided, so a panel can show it and a reader can tell "this is the agent on screen" from
        /// "this is the only one running".
        ///
        /// <para>
        /// Memoized, because this is asked four to eight times per repaint — <c>CurrentRecording</c> from both
        /// <c>OnGUI</c> and <c>GetHeight</c> in each of three panels, plus <c>DrawSourceControls</c> and
        /// <c>CurrentTopology</c> — and when neither the canvas nor the selection answers, <em>every one of
        /// those</em> falls through to <see cref="SingleRecordingMachine"/>'s <c>FindObjectsByType</c>. That
        /// is O(scene) several times per repaint of a window that repaints every frame, and it is worst in
        /// the multi-agent scene where the walk returns null anyway.
        /// </para>
        ///
        /// <para>
        /// Sharing one answer per frame is not only cheaper: it is what stops two panels resolving
        /// differently <em>within</em> a repaint, which is the disagreement this class exists to prevent.
        /// </para>
        /// </summary>
        public static BehaviorTreeMachine Resolve(GraphCore.IGraphContext context, out string source)
        {
            if (MemoAnswers(context, out var remembered))
            {
                source = memoSource;
                return remembered;
            }

            var machine = Decide(context, out source);

            memoContext = context;
            memoSelection = Selection.activeGameObject;
            memoMachine = new Remembered<BehaviorTreeMachine>(machine);
            memoSource = source;
            memoAt = EditorApplication.timeSinceStartup;
            memoValid = true;

            return machine;
        }

        /// <summary>
        /// Whether the remembered answer is still the answer.
        ///
        /// <para>
        /// The two inputs a user can change — which canvas is asking, and what is selected in the hierarchy —
        /// are compared directly rather than waited out, so nothing a user does is ever a quarter of a second
        /// late. The clock only bounds how stale a <em>scene walk</em> may be, which is the part that cannot
        /// be invalidated by watching anything cheap: a null answer means no single agent was recording, and
        /// nothing announces when that stops being true.
        /// </para>
        ///
        /// <para>
        /// A remembered machine is also re-checked for liveness every time. Skipping that would hand a
        /// destroyed machine to a panel for up to <see cref="MemoSeconds"/> — a cache reintroducing the
        /// exact failure the resolver is written to avoid.
        /// </para>
        /// </summary>
        private static bool MemoAnswers(GraphCore.IGraphContext context, out BehaviorTreeMachine remembered)
        {
            remembered = null;

            if (!memoValid) return false;
            if (!ReferenceEquals(memoContext, context)) return false;
            if (memoSelection != Selection.activeGameObject) return false;
            if (EditorApplication.timeSinceStartup - memoAt > MemoSeconds) return false;

            // A remembered machine that has been destroyed reads as null through Unity's operator, exactly
            // like a remembered "no agent is recording" -- which is why the two are told apart by
            // Remembered<T> rather than by a null check that cannot see the difference. Serving a destroyed
            // machine here would have a cache reintroducing the failure the resolver exists to avoid.
            if (!memoMachine.TryReuse(out remembered)) return false;

            // Detaching a recorder is the other way an answer stops being one, and no cheap key can see it.
            // Through IsRecording so the memo and the resolver agree on what "recording" means by
            // construction rather than by two copies of the same test staying in step.
            return remembered == null || IsRecording(remembered);
        }

        /// <summary>
        /// The agent to explain and how it was chosen, as one phrase, or null when nothing says which.
        ///
        /// <para>
        /// Worded once rather than per panel for the same reason the resolution is: two panels describing the
        /// same agent in different words is a reader's cue that they are looking at two different things.
        /// </para>
        /// </summary>
        public static string Describe(GraphCore.IGraphContext context)
        {
            var machine = Resolve(context, out var source);

            return machine != null ? $"{machine.name}  ({source})" : null;
        }

        /// <summary>
        /// Drops the memo, so the next ask resolves from scratch.
        ///
        /// <para>
        /// A static that survives a play-mode boundary is the shape of finding 1.2, and this one does — the
        /// clock keeps it to a quarter of a second, which is short enough not to be that bug and long enough
        /// that a boundary should still say so out loud. This is the seam for it: the play-mode handler
        /// added by the 1.2 fix should call it alongside the two statics it already clears.
        /// </para>
        /// </summary>
        public static void Forget()
        {
            memoValid = false;
            memoContext = null;
            memoSelection = null;
            memoMachine = default;
            memoSource = null;
            memoAt = double.NegativeInfinity;
        }

        private static BehaviorTreeMachine Decide(GraphCore.IGraphContext context, out string source)
        {
            // The canvas's own reference knows which machine it was opened through. This is the answer whenever
            // the tree is being watched live, which is the case the panels exist for.
            if (context?.reference != null)
            {
                var viewed = context.reference.machine as BehaviorTreeMachine;

                if (IsRecording(viewed))
                {
                    source = "shown on this canvas";
                    return viewed;
                }

                var owner = context.reference.gameObject;
                if (owner != null)
                {
                    var onOwner = owner.GetComponent<BehaviorTreeMachine>();

                    if (IsRecording(onOwner))
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

                if (IsRecording(onSelection))
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
        /// Whether this machine is something the panels can read a history from: alive, and holding a
        /// recorder.
        ///
        /// <para>
        /// The null test has to be the <see cref="UnityEngine.Object"/> one, and every route into this
        /// method used to bypass it in a different way -- an <c>is</c> pattern match, which succeeds on a
        /// destroyed machine, and <c>?.</c>, which is the C# null check rather than Unity's. A destroyed
        /// machine that answered here would be handed to a panel, which then either throws
        /// <c>MissingReferenceException</c> out of <c>OnGUI</c> when it reads <c>machine.name</c>, or draws
        /// a dead recording under a LIVE banner.
        /// </para>
        /// </summary>
        public static bool IsRecording(BehaviorTreeMachine machine)
        {
            return machine != null && machine.FlightRecorder != null;
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
                if (!IsRecording(machine)) continue;

                if (found != null) return null;

                found = machine;
            }

            return found;
        }
    }
}

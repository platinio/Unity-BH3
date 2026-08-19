using System;
using System.Collections.Generic;
using System.IO;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The timeline scrubber: what ran, when, and what the tree looked like at any tick in the buffer.
    ///
    /// <para>
    /// A renderer and an input surface, nothing more. The lanes come from <see cref="BehaviorTreeTimeline"/>
    /// and the canvas state from <see cref="BehaviorTreeTreeState"/>, both of which are pure C# over an
    /// <see cref="IBehaviorTreeRecording"/> — so a recording exported from someone else's playtest scrubs
    /// exactly as well as the agent running in front of you, and this class cannot develop its own opinion
    /// about what happened.
    /// </para>
    ///
    /// <para>
    /// Hosted in the bottom dock because a timeline's axis is time and a sidebar is a tall narrow column.
    /// It implements the same <see cref="ISidebarPanelContent"/> contract the sidebar uses, so moving it back
    /// there needs no change here.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeTimelinePanel : ISidebarPanelContent
    {
        private const float ToolbarHeight = 20.0f;
        private const float RulerHeight = 16.0f;
        private const float LaneHeight = 18.0f;
        private const float LaneSpacing = 2.0f;
        private const float LabelWidth = 132.0f;
        private const float Padding = 4.0f;
        private const float MinTickSpan = 8.0f;

        /// <summary>Machine ticks advanced per second of wall clock while playing back.</summary>
        private const float PlaybackTicksPerSecond = 60.0f;

        /// <summary>
        /// The panel editor-wide scrub requests move: the one that is actually on screen.
        ///
        /// <para>
        /// Claimed in <see cref="OnGUI"/> rather than in the constructor, because being constructed says
        /// nothing about being visible. Panels are built per <c>GraphContext</c>, contexts are rebuilt on
        /// every reference switch, and <c>GraphContext.Dispose</c> never disposes panels — so a
        /// constructor claim means "last constructed", which is a different panel from "the one you are
        /// looking at" in both of the cases that matter: two graph windows open, where the second window's
        /// collapsed timeline would take scrub requests aimed at the first window's visible one; and a
        /// window that has closed, whose panel would go on answering for the editor forever.
        /// </para>
        ///
        /// <para>
        /// Drawing is the only liveness signal available here, and it is the right one: a panel that draws
        /// is by definition the panel a moved playhead would be seen on. The claim is therefore
        /// self-healing — whatever stale value it holds is corrected by the next panel to draw.
        /// </para>
        /// </summary>
        private static BehaviorTreeTimelinePanel active;

        private BehaviorTreeRecordingSnapshot loaded;
        private string loadedFrom;

        private BehaviorTreeTimeline timeline;
        private int cachedEventCount = -1;
        private int cachedTick = -1;
        private object cachedSource;

        private BehaviorTreeTreeState state;
        private int stateTick = int.MinValue;

        /// <summary>-1 means live: follow the recording's newest tick and leave the canvas alone.</summary>
        private int scrubTick = -1;

        private bool playing;
        private double lastPlaybackTime;

        private float viewStart;
        private float viewSpan;
        private bool viewInitialised;

        private Vector2 laneScroll;

        public BehaviorTreeTimelinePanel(GraphCore.IGraphContext context)
        {
            this.context = context;

            titleContent = new GUIContent("Timeline", BoltCore.Icons.variablesWindow?[IconSize.Small]);
        }

        public GraphCore.IGraphContext context { get; }

        public object sidebarControlHint => typeof(BehaviorTreeTimelinePanel);

        public GUIContent titleContent { get; }

        public Vector2 minSize => new(420.0f, 120.0f);

        /// <summary>
        /// Where this panel's playhead sits, or -1 when it is live rather than scrubbing.
        /// </summary>
        public int ScrubTick => scrubTick;

        /// <summary>
        /// Takes the editor-wide scrub target for this panel. Called from <see cref="OnGUI"/>, which is what
        /// makes the claim mean "on screen" rather than "constructed" — see <see cref="active"/>.
        /// </summary>
        public void ClaimScrubTarget()
        {
            active = this;
        }

        /// <summary>
        /// Moves the scrubber from elsewhere in the editor. The why-inspector emits tick links for exactly
        /// this and nothing consumed them until now — clicking "aborted at tick 412" should take you there.
        ///
        /// <para>
        /// Silently does nothing until some timeline has drawn. That is the honest answer rather than a
        /// missing one: with no timeline on screen there is no playhead to move, and the alternative —
        /// arming a panel nobody is looking at — is how a request ends up landing on the wrong window.
        /// <c>BehaviorTreeBreakpointResponder.Reveal</c> already documents this no-op as the ordinary case
        /// of the timeline never having been opened; until the claim moved to <see cref="OnGUI"/> that
        /// comment described behaviour the code did not have.
        /// </para>
        /// </summary>
        public static void RequestScrub(int tick)
        {
            if (active == null || tick < 0) return;

            active.scrubTick = tick;
            active.playing = false;
            active.FocusOn(tick);
        }

        public float GetHeight(float width)
        {
            var lanes = timeline?.Lanes.Count ?? 0;

            return ToolbarHeight + RulerHeight + Padding * 3.0f
                   + Mathf.Max(1, lanes) * (LaneHeight + LaneSpacing);
        }

        public void OnGUI(Rect position)
        {
            // Before anything can return early: a panel drawing its "no agent is recording" message is still
            // the timeline on screen, and is still the one a tick link should move.
            ClaimScrubTarget();

            var recording = CurrentRecording();
            var toolbar = new Rect(position.x, position.y, position.width, ToolbarHeight);

            DrawToolbar(toolbar, recording);

            var body = new Rect(
                position.x,
                toolbar.yMax + Padding,
                position.width,
                position.height - ToolbarHeight - Padding);

            if (body.height <= 0.0f) return;

            if (recording == null)
            {
                EditorGUI.LabelField(body, Application.isPlaying
                    ? "No agent in this scene is recording. Check BehaviorTreeFlightRecorders.GloballyEnabled."
                    : "Enter play mode to watch an agent, or load an exported recording.", EditorStyles.miniLabel);

                BehaviorTreeScrubOverride.Clear();
                BehaviorTreeDebugSession.Clear();
                return;
            }

            timeline = TimelineFor(recording);

            if (timeline.IsEmpty)
            {
                EditorGUI.LabelField(body, "Nothing recorded yet.", EditorStyles.miniLabel);
                BehaviorTreeScrubOverride.Clear();

                // Published even while empty: the watch should name the same agent and say it has nothing yet,
                // rather than resolve a different one and look like it found something.
                BehaviorTreeDebugSession.Publish(recording, recording.Tick, false);
                return;
            }

            EnsureView();
            AdvancePlayback();
            SyncCanvas(recording);

            // After playback and scrubbing have settled, so the tick published is the one being drawn.
            BehaviorTreeDebugSession.Publish(recording, EffectiveTick, IsScrubbing);

            DrawBody(body, recording);
        }

        #region Sources

        /// <summary>
        /// The agent being scrubbed, and where that answer came from.
        ///
        /// <para>
        /// Not a picker, for a reason that is sharper here than in the why-inspector: scrubbing <em>ghosts the
        /// canvas</em>. Aiming a dropdown at a different agent would paint that agent's history onto the nodes
        /// of the one on screen — a confident, wrong picture rather than merely a confusing control.
        /// Resolution is shared with the why-inspector so the two cannot disagree about whose history is up.
        /// </para>
        /// </summary>
        private BehaviorTreeMachine CurrentMachine(out string source)
        {
            return BehaviorTreeDebugTarget.Resolve(context, out source);
        }

        private IBehaviorTreeRecording CurrentRecording()
        {
            if (loaded != null) return loaded;

            return CurrentMachine(out _)?.FlightRecorder;
        }

        private IBehaviorTreeTopology CurrentTopology()
        {
            // A loaded recording has no live graph, so it falls back to whatever tree is open on the canvas.
            // When that is the wrong tree, names simply do not resolve and the lanes fall back to guids rather
            // than labelling a bar with someone else's node name.
            if (loaded == null)
            {
                var machine = CurrentMachine(out _);
                if (machine != null) return BehaviorTreeGraphTopology.From(machine);
            }

            return context?.graph is BehaviorTreeGraph graph ? BehaviorTreeGraphTopology.From(graph) : null;
        }

        /// <summary>
        /// Rebuilt only when the recording actually moved. A live recorder grows every frame, so an uncached
        /// build would replay the whole ring twice per repaint for a panel that is open all session.
        /// </summary>
        private BehaviorTreeTimeline TimelineFor(IBehaviorTreeRecording recording)
        {
            if (timeline != null &&
                ReferenceEquals(cachedSource, recording) &&
                cachedEventCount == recording.EventCount &&
                cachedTick == recording.Tick)
            {
                return timeline;
            }

            // A different agent means a different clock. Carrying the playhead across would park it on a tick
            // number that means nothing in the new recording and ghost the canvas with it.
            if (!ReferenceEquals(cachedSource, recording))
            {
                GoLive();
                viewInitialised = false;
            }

            cachedSource = recording;
            cachedEventCount = recording.EventCount;
            cachedTick = recording.Tick;
            stateTick = int.MinValue;

            return BehaviorTreeTimeline.Build(recording, CurrentTopology());
        }

        #endregion

        #region Canvas

        private bool IsScrubbing => scrubTick >= 0;

        private int EffectiveTick => IsScrubbing ? scrubTick : timeline?.LastTick ?? 0;

        /// <summary>
        /// Pushes the reconstructed moment at the scrub tick onto the canvas, or hands the canvas back when
        /// live. Nothing here touches a node — see <see cref="BehaviorTreeScrubOverride"/>.
        /// </summary>
        private void SyncCanvas(IBehaviorTreeRecording recording)
        {
            if (!IsScrubbing)
            {
                BehaviorTreeScrubOverride.Clear();
                return;
            }

            if (stateTick != scrubTick || state == null)
            {
                // Reuses the timeline already built this frame, so scrubbing does not replay the ring twice
                // per repaint — and guarantees the bar under the playhead and the lit node agree.
                state = BehaviorTreeTreeState.At(recording, timeline, scrubTick);
                stateTick = scrubTick;
            }

            BehaviorTreeScrubOverride.Set(state, recording.AgentName, recording);
        }

        private void GoLive()
        {
            scrubTick = -1;
            playing = false;
            state = null;
            stateTick = int.MinValue;

            BehaviorTreeScrubOverride.Clear();
        }

        private void ScrubTo(int tick)
        {
            scrubTick = Mathf.Clamp(tick, timeline.FirstTick, timeline.LastTick);
        }

        #endregion

        #region Playback

        private void AdvancePlayback()
        {
            if (!playing)
            {
                lastPlaybackTime = EditorApplication.timeSinceStartup;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var elapsed = now - lastPlaybackTime;
            lastPlaybackTime = now;

            var advance = Mathf.RoundToInt((float)elapsed * PlaybackTicksPerSecond);
            if (advance <= 0) return;

            var next = EffectiveTick + advance;

            // Running off the end returns to live rather than sticking at the last tick — the recording is
            // still growing, and a scrubber parked on the newest tick pretending to be paused is a lie.
            if (next >= timeline.LastTick)
            {
                GoLive();
                return;
            }

            ScrubTo(next);
            FocusOn(scrubTick);
        }

        private void StepChange(int direction)
        {
            var ticks = timeline.ChangeTicks;
            if (ticks.Count == 0) return;

            var from = EffectiveTick;

            if (direction > 0)
            {
                for (int i = 0; i < ticks.Count; i++)
                {
                    if (ticks[i] <= from) continue;

                    playing = false;
                    ScrubTo(ticks[i]);
                    FocusOn(scrubTick);
                    return;
                }

                GoLive();
                return;
            }

            for (int i = ticks.Count - 1; i >= 0; i--)
            {
                if (ticks[i] >= from) continue;

                playing = false;
                ScrubTo(ticks[i]);
                FocusOn(scrubTick);
                return;
            }
        }

        #endregion

        #region View

        private void EnsureView()
        {
            var span = Mathf.Max(MinTickSpan, timeline.LastTick - timeline.FirstTick);

            if (!viewInitialised)
            {
                viewStart = timeline.FirstTick;
                viewSpan = span;
                viewInitialised = true;
                return;
            }

            viewSpan = Mathf.Clamp(viewSpan, MinTickSpan, span);
            viewStart = Mathf.Clamp(viewStart, timeline.FirstTick, Mathf.Max(timeline.FirstTick, timeline.LastTick - viewSpan));

            // Live playback keeps the newest tick on screen; scrubbing leaves the view where it was put.
            if (!IsScrubbing) viewStart = Mathf.Max(timeline.FirstTick, timeline.LastTick - viewSpan);
        }

        private void FocusOn(int tick)
        {
            if (timeline == null) return;
            if (tick >= viewStart && tick <= viewStart + viewSpan) return;

            viewStart = Mathf.Clamp(
                tick - viewSpan * 0.5f,
                timeline.FirstTick,
                Mathf.Max(timeline.FirstTick, timeline.LastTick - viewSpan));
        }

        private float TickToX(float tick, Rect track)
        {
            return track.x + (tick - viewStart) / Mathf.Max(1.0f, viewSpan) * track.width;
        }

        private int XToTick(float x, Rect track)
        {
            var normalised = Mathf.Clamp01((x - track.x) / Mathf.Max(1.0f, track.width));

            return Mathf.RoundToInt(viewStart + normalised * viewSpan);
        }

        #endregion

        #region Toolbar

        private void DrawToolbar(Rect area, IBehaviorTreeRecording recording)
        {
            GUI.Label(area, GUIContent.none, LudiqStyles.toolbarBackground);

            var x = area.x + 2.0f;

            using (new EditorGUI.DisabledScope(recording == null))
            {
                if (GUI.Button(new Rect(x, area.y, 26.0f, area.height), playing ? "❚❚" : "▶", EditorStyles.miniButton))
                {
                    playing = !playing;

                    // Pressing play while live rewinds to the start, which is what "play the recording" means.
                    if (playing && !IsScrubbing) ScrubTo(timeline?.FirstTick ?? 0);

                    lastPlaybackTime = EditorApplication.timeSinceStartup;
                }

                x += 28.0f;

                if (GUI.Button(new Rect(x, area.y, 26.0f, area.height), "⏮", EditorStyles.miniButton)) StepChange(-1);
                x += 26.0f;

                if (GUI.Button(new Rect(x, area.y, 26.0f, area.height), "◀", EditorStyles.miniButton))
                {
                    playing = false;
                    ScrubTo(EffectiveTick - 1);
                    FocusOn(scrubTick);
                }

                x += 26.0f;

                if (GUI.Button(new Rect(x, area.y, 26.0f, area.height), "▶|", EditorStyles.miniButton))
                {
                    playing = false;
                    ScrubTo(EffectiveTick + 1);
                    FocusOn(scrubTick);
                }

                x += 26.0f;

                if (GUI.Button(new Rect(x, area.y, 26.0f, area.height), "⏭", EditorStyles.miniButton)) StepChange(1);
                x += 30.0f;
            }

            // The banner. While scrubbing, the canvas is showing history, and that has to be impossible to
            // miss — a ghosted canvas mistaken for a live one is worse than no scrubber at all.
            var bannerWidth = 260.0f;
            var banner = new Rect(x, area.y, bannerWidth, area.height);

            if (IsScrubbing)
            {
                EditorGUI.DrawRect(banner, new Color(0.65f, 0.35f, 0.05f, 0.85f));
                GUI.Label(banner, $"  ⏸ SCRUBBING @ tick {scrubTick} — canvas shows history", EditorStyles.whiteMiniLabel);
                x += bannerWidth + 4.0f;

                if (GUI.Button(new Rect(x, area.y, 84.0f, area.height), "Return to live", EditorStyles.miniButton))
                {
                    GoLive();
                }

                x += 88.0f;
            }
            else
            {
                GUI.Label(banner, $"  ● LIVE — tick {timeline?.LastTick ?? 0}", EditorStyles.miniLabel);
                x += bannerWidth + 4.0f;
            }

            DrawSourceControls(new Rect(x, area.y, Mathf.Max(0.0f, area.xMax - x - 2.0f), area.height));
        }

        private void DrawSourceControls(Rect area)
        {
            if (area.width < 120.0f) return;

            var buttonWidth = 52.0f;
            var picker = new Rect(area.x, area.y, area.width - buttonWidth * 2.0f - 4.0f, area.height);

            if (loaded != null)
            {
                GUI.Label(picker, $"File: {loadedFrom}", EditorStyles.miniLabel);

                if (GUI.Button(new Rect(area.xMax - buttonWidth, area.y, buttonWidth, area.height), "Close", EditorStyles.miniButton))
                {
                    loaded = null;
                    loadedFrom = null;
                    GoLive();
                    Invalidate();
                }

                return;
            }

            // A label, not a control: it states which agent is on the timeline and how that was decided, so a
            // reader can tell "the one this canvas is showing" from "the only one running".
            var machine = CurrentMachine(out var source);

            GUI.Label(
                picker,
                machine != null ? $"{machine.name}  ({source})" : "No agent — nothing says which one to scrub",
                EditorStyles.miniLabel);

            if (GUI.Button(new Rect(area.xMax - buttonWidth, area.y, buttonWidth, area.height), "Load…", EditorStyles.miniButton))
            {
                Load();
            }
        }

        #endregion

        #region Body

        private void DrawBody(Rect body, IBehaviorTreeRecording recording)
        {
            var track = new Rect(body.x + LabelWidth, body.y, body.width - LabelWidth - Padding, body.height);

            HandleTrackInput(track);

            DrawRuler(new Rect(track.x, body.y, track.width, RulerHeight));

            var lanesArea = new Rect(body.x, body.y + RulerHeight, body.width, body.height - RulerHeight);
            var contentHeight = timeline.Lanes.Count * (LaneHeight + LaneSpacing);

            var view = new Rect(0.0f, 0.0f, lanesArea.width - 16.0f, contentHeight);
            laneScroll = GUI.BeginScrollView(lanesArea, laneScroll, view, false, contentHeight > lanesArea.height);
            {
                var scrolledTrack = new Rect(LabelWidth, 0.0f, track.width, contentHeight);

                for (int i = 0; i < timeline.Lanes.Count; i++)
                {
                    var lane = timeline.Lanes[i];
                    var row = new Rect(0.0f, i * (LaneHeight + LaneSpacing), lanesArea.width, LaneHeight);

                    DrawLane(lane, row, scrolledTrack);
                }

                DrawMarkers(scrolledTrack, contentHeight);
                DrawPlayhead(scrolledTrack, contentHeight);
            }
            GUI.EndScrollView();

            DrawFooter(recording, body);
        }

        private void DrawRuler(Rect area)
        {
            EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f)
                : new Color(0.72f, 0.72f, 0.72f));

            // Roughly one label per 90px, snapped to a round number so the labels do not jitter while playing.
            var target = Mathf.Max(1.0f, viewSpan / Mathf.Max(1.0f, area.width / 90.0f));
            var magnitude = Mathf.Pow(10.0f, Mathf.Floor(Mathf.Log10(target)));
            var step = Mathf.Max(1.0f, Mathf.Ceil(target / magnitude) * magnitude);

            var first = Mathf.Ceil(viewStart / step) * step;

            for (var tick = first; tick <= viewStart + viewSpan; tick += step)
            {
                var x = TickToX(tick, area);
                if (x < area.x || x > area.xMax) continue;

                EditorGUI.DrawRect(new Rect(x, area.yMax - 4.0f, 1.0f, 4.0f), new Color(0.5f, 0.5f, 0.5f));
                GUI.Label(new Rect(x + 2.0f, area.y - 1.0f, 70.0f, area.height), ((int)tick).ToString(), EditorStyles.miniLabel);
            }
        }

        private void DrawLane(BehaviorTreeTimelineLane lane, Rect row, Rect track)
        {
            var label = new Rect(row.x + 2.0f, row.y, LabelWidth - 6.0f, row.height);
            GUI.Label(label, $"depth {lane.Depth}", EditorStyles.miniLabel);

            var laneTrack = new Rect(track.x, row.y, track.width, row.height);

            EditorGUI.DrawRect(laneTrack, EditorGUIUtility.isProSkin
                ? new Color(0.16f, 0.16f, 0.16f)
                : new Color(0.82f, 0.82f, 0.82f));

            foreach (var segment in lane.Segments)
            {
                DrawSegment(segment, laneTrack);
            }
        }

        private void DrawSegment(BehaviorTreeTimelineSegment segment, Rect laneTrack)
        {
            var end = segment.EndTickOr(timeline.LastTick);

            var left = TickToX(segment.EnterTick, laneTrack);
            var right = TickToX(end, laneTrack);

            if (right < laneTrack.x || left > laneTrack.xMax) return;

            left = Mathf.Max(left, laneTrack.x);
            right = Mathf.Min(right, laneTrack.xMax);

            // A branch that entered and died on the same tick still happened; a zero-width bar is a bug report
            // nobody can find again.
            var bar = new Rect(left, laneTrack.y + 1.0f, Mathf.Max(2.0f, right - left), laneTrack.height - 2.0f);

            DrawSegmentFill(segment, bar, end, laneTrack);

            if (bar.width > 34.0f && !string.IsNullOrEmpty(segment.Name))
            {
                GUI.Label(new Rect(bar.x + 3.0f, bar.y - 1.0f, bar.width - 6.0f, bar.height), segment.Name, EditorStyles.miniLabel);
            }

            GUI.Label(bar, new GUIContent(string.Empty, TooltipFor(segment, end)));

            if (Event.current.type == EventType.MouseDown &&
                Event.current.button == 0 &&
                Event.current.clickCount == 2 &&
                bar.Contains(Event.current.mousePosition))
            {
                SelectOnCanvas(segment.NodeGuid);
                Event.current.Use();
            }
        }

        /// <summary>
        /// Fills a bar in two parts, split at the playhead.
        ///
        /// <para>
        /// The whole bar used to be painted with the segment's <em>final</em> outcome, which quietly broke the
        /// one promise the scrubber makes. Parked at tick 0, a Selector that would not finish until tick 18
        /// read "Succeeded" — with nothing inside it, because its children had not been chosen yet — so it
        /// looked like a Selector that had somehow succeeded on its own. The engine never believed that
        /// (<see cref="BehaviorTreeTreeState"/> reports Running before the exit tick, which is why the ghosted
        /// canvas was right), so the bar and the canvas disagreed.
        /// </para>
        ///
        /// <para>
        /// Now the part up to the playhead is coloured by what was true <em>then</em>, and the part after it is
        /// dimmed as the future you have not scrubbed to. Live, the playhead sits at the last tick, so there is
        /// no future and the bars look exactly as they did.
        /// </para>
        /// </summary>
        private void DrawSegmentFill(BehaviorTreeTimelineSegment segment, Rect bar, int end, Rect laneTrack)
        {
            var playhead = EffectiveTick;
            var outcome = ColourFor(segment.Outcome);

            // Not entered yet at this moment: show the whole thing as future so the lane still reads, but
            // nothing about it claims to have happened.
            if (segment.EnterTick > playhead)
            {
                EditorGUI.DrawRect(bar, Dimmed(outcome));
                return;
            }

            var endedByNow = !segment.IsOpen && segment.ExitTick <= playhead;
            var past = endedByNow ? outcome : ColourFor(BehaviorTreeOutcome.Running);

            if (endedByNow || playhead >= end)
            {
                EditorGUI.DrawRect(bar, past);
                return;
            }

            var splitX = Mathf.Clamp(TickToX(playhead, laneTrack), bar.x, bar.xMax);

            EditorGUI.DrawRect(new Rect(bar.x, bar.y, Mathf.Max(1.0f, splitX - bar.x), bar.height), past);
            EditorGUI.DrawRect(new Rect(splitX, bar.y, Mathf.Max(0.0f, bar.xMax - splitX), bar.height), Dimmed(outcome));
        }

        /// <summary>
        /// What the bar says on hover, from the playhead's point of view rather than the recording's end.
        /// A node still running at the scrubbed tick says so, and names the outcome as something that happens
        /// later rather than something that is already true.
        /// </summary>
        private string TooltipFor(BehaviorTreeTimelineSegment segment, int end)
        {
            var playhead = EffectiveTick;
            var callSite = segment.CallSiteId != BehaviorTreeCallSite.RootId
                ? $"  ·  call site {segment.CallSiteId}"
                : string.Empty;

            if (segment.EnterTick > playhead)
            {
                return $"{segment.Name}  ·  has not entered yet at tick {playhead}"
                       + $"  ·  enters at {segment.EnterTick}{callSite}";
            }

            if (!segment.IsOpen && segment.ExitTick <= playhead)
            {
                return $"{segment.Name}  ·  {segment.Outcome}  ·  ran {segment.EnterTick}–{end}{callSite}";
            }

            var ending = segment.IsOpen
                ? "still running at the end of the recording"
                : $"this run ended {segment.Outcome} at tick {end}";

            return $"{segment.Name}  ·  Running at tick {playhead}  ·  entered at {segment.EnterTick}"
                   + $"  ·  {ending}{callSite}";
        }

        private static Color Dimmed(Color colour)
        {
            return new Color(colour.r, colour.g, colour.b, colour.a * 0.22f);
        }

        private void DrawMarkers(Rect track, float height)
        {
            foreach (var marker in timeline.Markers)
            {
                var x = TickToX(marker.Tick, track);
                if (x < track.x || x > track.xMax) continue;

                var colour = marker.Kind == BehaviorTreeTimelineMarkerKind.Abort
                    ? new Color(0.95f, 0.25f, 0.2f)
                    : new Color(0.45f, 0.7f, 1.0f);

                var y = marker.Depth * (LaneHeight + LaneSpacing);

                EditorGUI.DrawRect(new Rect(x - 1.0f, 0.0f, 2.0f, height), new Color(colour.r, colour.g, colour.b, 0.25f));

                var pin = new Rect(x - 3.0f, y, 6.0f, LaneHeight);
                EditorGUI.DrawRect(pin, colour);

                var what = marker.Kind == BehaviorTreeTimelineMarkerKind.Abort
                    ? $"Aborted at tick {marker.Tick} by guard '{marker.Label}'"
                    : $"{(marker.Kind == BehaviorTreeTimelineMarkerKind.TreePushed ? "Entered" : "Left")} sub-tree '{marker.Label}' at tick {marker.Tick}";

                GUI.Label(pin, new GUIContent(string.Empty, what));

                if (Event.current.type == EventType.MouseDown &&
                    Event.current.clickCount == 2 &&
                    pin.Contains(Event.current.mousePosition))
                {
                    if (marker.RelatedGuid != Guid.Empty) SelectOnCanvas(marker.RelatedGuid);
                    else SelectOnCanvas(marker.NodeGuid);

                    Event.current.Use();
                }
            }
        }

        private void DrawPlayhead(Rect track, float height)
        {
            var x = TickToX(EffectiveTick, track);
            if (x < track.x || x > track.xMax) return;

            EditorGUI.DrawRect(new Rect(x - 1.0f, 0.0f, 2.0f, height),
                IsScrubbing ? new Color(1.0f, 0.65f, 0.1f) : new Color(0.4f, 0.9f, 0.4f));
        }

        private void DrawFooter(IBehaviorTreeRecording recording, Rect body)
        {
            if (timeline.Dropped <= 0) return;

            var note = new Rect(body.x + 2.0f, body.yMax - 14.0f, body.width - 4.0f, 14.0f);

            GUI.Label(note,
                $"Buffer clipped: {timeline.Dropped} older events dropped — the left edge is a cut, not a beginning.",
                EditorStyles.miniLabel);
        }

        private static Color ColourFor(BehaviorTreeOutcome outcome)
        {
            switch (outcome)
            {
                case BehaviorTreeOutcome.Running: return new Color(0.25f, 0.55f, 0.85f);
                case BehaviorTreeOutcome.Succeeded: return new Color(0.3f, 0.65f, 0.35f);
                case BehaviorTreeOutcome.Failed: return new Color(0.75f, 0.5f, 0.15f);
                case BehaviorTreeOutcome.Aborted: return new Color(0.8f, 0.25f, 0.2f);
                default: return new Color(0.45f, 0.45f, 0.45f);
            }
        }

        #endregion

        #region Input

        private void HandleTrackInput(Rect track)
        {
            var e = Event.current;
            if (!track.Contains(e.mousePosition)) return;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && e.clickCount == 1:
                case EventType.MouseDrag when e.button == 0:
                    playing = false;
                    ScrubTo(XToTick(e.mousePosition.x, track));
                    e.Use();
                    break;

                case EventType.ScrollWheel:
                    // Zoom about the cursor, so the tick under the pointer stays put.
                    var anchor = XToTick(e.mousePosition.x, track);
                    var factor = Mathf.Pow(1.15f, e.delta.y);
                    var span = Mathf.Max(timeline.LastTick - timeline.FirstTick, MinTickSpan);

                    viewSpan = Mathf.Clamp(viewSpan * factor, MinTickSpan, span);
                    viewStart = Mathf.Clamp(
                        anchor - (anchor - viewStart) * factor,
                        timeline.FirstTick,
                        Mathf.Max(timeline.FirstTick, timeline.LastTick - viewSpan));

                    e.Use();
                    break;

                case EventType.MouseDrag when e.button == 2:
                    viewStart = Mathf.Clamp(
                        viewStart - e.delta.x / Mathf.Max(1.0f, track.width) * viewSpan,
                        timeline.FirstTick,
                        Mathf.Max(timeline.FirstTick, timeline.LastTick - viewSpan));

                    e.Use();
                    break;
            }
        }

        private void SelectOnCanvas(Guid guid)
        {
            if (context?.graph is not BehaviorTreeGraph graph) return;

            foreach (var node in graph.Nodes)
            {
                if (node == null || node.guid != guid) continue;

                context.selection.Select(node);
                return;
            }
        }

        #endregion

        #region Files

        private void Load()
        {
            var path = EditorUtility.OpenFilePanel("Load recording", "", "json");
            if (string.IsNullOrEmpty(path)) return;

            if (!BehaviorTreeRecordingImport.TryFromJson(File.ReadAllText(path), out var recording))
            {
                EditorUtility.DisplayDialog("Load recording", "That file is not a behavior tree recording.", "OK");
                return;
            }

            loaded = recording;
            loadedFrom = Path.GetFileName(path);

            GoLive();
            Invalidate();
        }

        private void Invalidate()
        {
            timeline = null;
            cachedSource = null;
            cachedEventCount = -1;
            cachedTick = -1;
            stateTick = int.MinValue;
            viewInitialised = false;
        }

        #endregion
    }
}

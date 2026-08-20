using System;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Reads a live recording back in English.
    ///
    /// <para>
    /// The recorder stores guids and enums and never formats anything, which is what keeps it allocation-free
    /// on the hot path. The cost lands here: a recording is unreadable until something turns those guids into
    /// node names, and the only place that mapping exists at runtime is the machine's own instantiated graph.
    /// That is the whole job of this window — plus the fact toggles, so the agent can be made to change its
    /// mind on demand rather than on a timer.
    /// </para>
    ///
    /// <para>
    /// This is a viewer, not the timeline scrubber (Component 2) or the why-inspector (Component 3). It shows
    /// events in order and resolves names; it does not reconstruct tree state at a tick or assemble causal
    /// sentences. It exists so Component 1 can be judged before either of those is built, and the guid
    /// resolution in it is the part worth promoting when they are.
    /// </para>
    /// </summary>
    public sealed class FlightRecorderWindow : EditorWindow
    {
        private const int MaxRows = 400;

        private int selectedAgent;
        private Vector2 scroll;
        private bool followTail = true;
        private BehaviorTreeEventKind kindFilter = BehaviorTreeEventKind.None;

        /// <summary>guid to the live node, built from the machine's graph including every sub-tree.</summary>
        private readonly Dictionary<Guid, BehaviorTreeNode> nodesByGuid = new();
        private BehaviorTreeFlightRecorder indexedFor;

        [MenuItem("Tools/BH3/Flight Recorder", priority = 0)]
        public static void Open()
        {
            GetWindow<FlightRecorderWindow>("Flight Recorder").Show();
        }

        /// <summary>Play mode moves on its own, so the window has to repaint on its own too.</summary>
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            var recorders = BehaviorTreeFlightRecorders.Active;

            if (recorders.Count == 0)
            {
                DrawEmptyState();
                return;
            }

            selectedAgent = Mathf.Clamp(selectedAgent, 0, recorders.Count - 1);
            var recorder = recorders[selectedAgent];

            DrawToolbar(recorders, recorder);
            DrawFacts(recorder);
            DrawEvents(recorder);
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                Application.isPlaying
                    ? "No agent is recording. A BehaviorTreeMachine registers a recorder in Awake, so either "
                      + "there is no machine in the scene or recording is globally disabled."
                    : "Nothing is recording yet. Enter play mode with a BehaviorTreeMachine in the scene.",
                MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("Build the demo scene"))
            {
                FlightRecorderDemoBuilder.Build();
            }

            BehaviorTreeFlightRecorders.GloballyEnabled = EditorGUILayout.ToggleLeft(
                "Recording globally enabled", BehaviorTreeFlightRecorders.GloballyEnabled);
        }

        private void DrawToolbar(IReadOnlyList<BehaviorTreeFlightRecorder> recorders, BehaviorTreeFlightRecorder recorder)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var names = new string[recorders.Count];
                for (int i = 0; i < recorders.Count; i++)
                {
                    names[i] = $"{recorders[i].AgentName} ({recorders[i].TreeName})";
                }

                selectedAgent = EditorGUILayout.Popup(selectedAgent, names, EditorStyles.toolbarPopup, GUILayout.Width(220.0f));

                recorder.Enabled = GUILayout.Toggle(
                    recorder.Enabled, recorder.Enabled ? "Recording" : "Paused", EditorStyles.toolbarButton, GUILayout.Width(80.0f));

                kindFilter = (BehaviorTreeEventKind) EditorGUILayout.EnumPopup(
                    kindFilter, EditorStyles.toolbarPopup, GUILayout.Width(110.0f));

                followTail = GUILayout.Toggle(followTail, "Follow", EditorStyles.toolbarButton, GUILayout.Width(60.0f));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton)) recorder.Clear();

                if (GUILayout.Button("Copy JSON", EditorStyles.toolbarButton))
                {
                    EditorGUIUtility.systemCopyBuffer = BehaviorTreeRecordingDump.ToJson(recorder);
                    ShowNotification(new GUIContent("Recording copied"));
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    $"tick {recorder.Tick}   ·   {recorder.Events.Count}/{recorder.Events.Capacity} events"
                    + (recorder.Events.Dropped > 0 ? $"   ·   {recorder.Events.Dropped} dropped" : "")
                    + $"   ·   {recorder.CallSites.Count} call site(s)",
                    EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// The facts the demo tree's guards read, as toggles.
        /// <para>
        /// Writing through <see cref="BehaviorTreeFlightRecorder.ExternalVariableWrite"/> rather than just
        /// setting the variable is the point, not a detail: it is how a perception sensor is meant to publish,
        /// and it is what puts a named writer in the recording. Set the variable silently and the recording
        /// shows a guard changing its mind for no visible reason — which is exactly the bug this feature
        /// exists to make impossible.
        /// </para>
        /// </summary>
        private void DrawFacts(BehaviorTreeFlightRecorder recorder)
        {
            var machine = FindMachine(recorder);
            if (machine == null || machine.Variables == null) return;

            var declarations = machine.Variables.declarations;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Agent facts", EditorStyles.boldLabel);

                foreach (var declaration in declarations)
                {
                    if (declaration.value is not bool current) continue;

                    bool next = EditorGUILayout.ToggleLeft(declaration.name, current);
                    if (next == current) continue;

                    recorder.ExternalVariableWrite("Inspector", declaration.name, current, next);
                    declarations.Set(declaration.name, next);
                }
            }
        }

        private void DrawEvents(BehaviorTreeFlightRecorder recorder)
        {
            EnsureIndex(recorder);

            var events = recorder.Events;
            int first = Mathf.Max(0, events.Count - MaxRows);

            if (followTail) scroll.y = float.MaxValue;

            using var scope = new EditorGUILayout.ScrollViewScope(scroll);
            scroll = scope.scrollPosition;

            int lastTick = int.MinValue;

            for (int i = first; i < events.Count; i++)
            {
                var recorded = events[i];
                if (kindFilter != BehaviorTreeEventKind.None && recorded.Kind != kindFilter) continue;

                // A blank line per tick, because the unit a reader thinks in is the tick, not the event.
                if (recorded.Tick != lastTick)
                {
                    if (lastTick != int.MinValue) EditorGUILayout.Space(4.0f);
                    lastTick = recorded.Tick;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{recorded.Tick}.{recorded.Sequence}", EditorStyles.miniLabel, GUILayout.Width(48.0f));

                    var previous = GUI.color;
                    GUI.color = ColorFor(recorded.Kind);
                    EditorGUILayout.LabelField(recorded.Kind.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(96.0f));
                    GUI.color = previous;

                    EditorGUILayout.LabelField(Describe(recorded, recorder), EditorStyles.miniLabel);
                }
            }
        }

        /// <summary>
        /// One event as a sentence. Mirrors the per-kind table on <see cref="BehaviorTreeEvent"/> — which is
        /// the reason that table exists, since every field here means something different per kind.
        /// </summary>
        private string Describe(in BehaviorTreeEvent recorded, BehaviorTreeFlightRecorder recorder)
        {
            string where = CallSiteLabel(recorded.CallSiteId, recorder);

            switch (recorded.Kind)
            {
                case BehaviorTreeEventKind.NodeEnter:
                    return $"{Name(recorded.NodeGuid)} started{where}";

                case BehaviorTreeEventKind.NodeExit:
                    return $"{Name(recorded.NodeGuid)} ended {recorded.Status}{where}";

                case BehaviorTreeEventKind.NodeAborted:
                    return $"{Name(recorded.NodeGuid)} killed mid-run by guard {Name(recorded.RelatedGuid)}{where}";

                case BehaviorTreeEventKind.NodeSkipped:
                    return $"{Name(recorded.NodeGuid)} refused entry by guard {Name(recorded.RelatedGuid)}{where}";

                case BehaviorTreeEventKind.GuardEval:
                    // The inverted one: the subject is the guard, and RelatedGuid is what it protects.
                    return $"guard {Name(recorded.NodeGuid)} → {(recorded.Flag ? "true" : "false")} "
                           + $"(protects {Name(recorded.RelatedGuid)}){where}";

                case BehaviorTreeEventKind.VariableWrite:
                    string writer = recorded.RelatedGuid != Guid.Empty
                        ? Name(recorded.RelatedGuid)
                        : recorded.Writer ?? "(unknown)";
                    return $"{recorded.Key}: {recorded.OldValue} → {recorded.NewValue}   by {writer}";

                case BehaviorTreeEventKind.TreePushed:
                    return $"entered sub-tree {recorded.Key} via {Name(recorded.NodeGuid)}";

                case BehaviorTreeEventKind.TreePopped:
                    return $"left sub-tree {recorded.Key} via {Name(recorded.NodeGuid)}";

                default:
                    return Name(recorded.NodeGuid);
            }
        }

        private string CallSiteLabel(int callSiteId, BehaviorTreeFlightRecorder recorder)
        {
            // Silent for the root, because saying "in the root tree" on every line of a tree with no sub-trees
            // is noise. It only earns its space once there is more than one place to be.
            if (callSiteId == BehaviorTreeCallSite.RootId) return string.Empty;

            foreach (var callSite in recorder.CallSites)
            {
                if (callSite.Id != callSiteId) continue;

                return $"   [in {callSite.AssetName} #{callSite.Id}, run by {Name(callSite.RunNodeGuid)}]";
            }

            return $"   [call site {callSiteId}]";
        }

        private string Name(Guid guid)
        {
            if (guid == Guid.Empty) return "(none)";
            if (!nodesByGuid.TryGetValue(guid, out var node) || node == null) return guid.ToString().Substring(0, 8);

            // Every guard is called "Boolean Conditional Execution", which tells a reader nothing. What a
            // designer actually recognises is the condition it reads, so a guard is named by its source.
            if (node is BooleanConditionalExecution guard) return $"[{SourceName(guard.Value)}]";

            return node.NodeName;
        }

        /// <summary>
        /// Follows a value input back to the node that originates it, and names that.
        /// <para>
        /// Guards are usually fed through a chain — a variable read, sometimes a <see cref="Not"/> — and the
        /// interesting name is at the far end. Walking to the terminal source finds it without this needing to
        /// know which node types are pass-through.
        /// </para>
        /// </summary>
        private static string SourceName(ValueInput port)
        {
            const int maxHops = 6;

            var current = port;

            for (int hop = 0; hop < maxHops; hop++)
            {
                var connection = current?.connection;
                if (connection?.source?.behaviorTreeNode is not BehaviorTreeNode source) break;

                ValueInput next = null;
                foreach (var input in source.valueInputs)
                {
                    if (input.connection == null) continue;

                    next = input;
                    break;
                }

                if (next == null) return source.NodeName;

                current = next;
            }

            return "unresolved condition";
        }

        #region Index

        /// <summary>
        /// Builds guid to name from the machine's live graph, descending into every sub-tree instance.
        /// <para>
        /// Two call sites on one branch asset hold nodes with identical guids, so this map is genuinely
        /// many-to-one. That is fine for a name — both really are called the same thing — and it is why the
        /// call site has to be carried separately rather than folded into the name.
        /// </para>
        /// </summary>
        private void EnsureIndex(BehaviorTreeFlightRecorder recorder)
        {
            if (ReferenceEquals(indexedFor, recorder) && nodesByGuid.Count > 0) return;

            nodesByGuid.Clear();
            indexedFor = recorder;

            // Through the machine's own answer rather than its cloned asset: a tree authored into the scene
            // has no clone, and this window used to equate the two independently of everything else that
            // asks. An embedded-graph agent's whole event log read as truncated guids because of it.
            var machine = FindMachine(recorder);
            if (machine == null) return;

            Index(machine.RunningGraph, 0);
        }

        private void Index(BehaviorTreeGraph graph, int depth)
        {
            if (graph == null || depth > 8) return;

            foreach (var node in graph.Nodes)
            {
                if (node == null) continue;

                nodesByGuid[node.guid] = node;

                // Reading the instance is safe here only because play mode has already built it; the machine
                // instantiates every sub-tree in Awake. Touching it outside play mode would clone the asset.
                if (node is RunBehaviorTreeGraphNode runNode && Application.isPlaying)
                {
                    Index(runNode.BehaviorTreeGraphInstance, depth + 1);
                }
            }
        }

        private static BehaviorTreeMachine FindMachine(BehaviorTreeFlightRecorder recorder)
        {
            foreach (var machine in FindObjectsByType<BehaviorTreeMachine>(FindObjectsSortMode.None))
            {
                if (ReferenceEquals(machine.FlightRecorder, recorder)) return machine;
            }

            return null;
        }

        #endregion

        private static Color ColorFor(BehaviorTreeEventKind kind)
        {
            switch (kind)
            {
                case BehaviorTreeEventKind.NodeAborted: return new Color(1.0f, 0.45f, 0.4f);
                case BehaviorTreeEventKind.NodeSkipped: return new Color(0.85f, 0.7f, 0.35f);
                case BehaviorTreeEventKind.GuardEval: return new Color(0.5f, 0.8f, 1.0f);
                case BehaviorTreeEventKind.VariableWrite: return new Color(0.7f, 1.0f, 0.7f);
                default: return Color.white;
            }
        }
    }
}

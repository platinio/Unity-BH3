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
    /// The why-inspector: select a node, read why it did what it did.
    ///
    /// <para>
    /// A rendering layer and nothing more. Every sentence comes from
    /// <see cref="BehaviorTreeExplainer"/>, which knows nothing about the editor — so the same answer is
    /// available from a test, a CLI, or an authoring agent reading a replay, and this panel cannot develop its
    /// own opinion about what happened.
    /// </para>
    ///
    /// <para>
    /// It reads an <see cref="IBehaviorTreeRecording"/> rather than a live agent, which is why a recording
    /// loaded from a file explains exactly as well as the one running in front of you.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeWhyPanel : ISidebarPanelContent
    {
        private const float Padding = 6.0f;
        private const float RowSpacing = 4.0f;
        private const float LinkButtonWidth = 26.0f;
        private const float SnapshotButtonWidth = 44.0f;

        private BehaviorTreeRecordingSnapshot loaded;
        private string loadedFrom;

        private int callSiteIndex;

        private BehaviorTreeExplanation cached;
        private IBehaviorTreeRecording cachedRecording;
        private Guid cachedNode;
        private int cachedScope = -1;
        private int cachedTick = -1;
        private int cachedEventCount = -1;

        private GUIStyle wrapped;
        private GUIStyle headline;
        private GUIStyle role;

        public BehaviorTreeWhyPanel(GraphCore.IGraphContext context)
        {
            this.context = context;

            titleContent = new GUIContent("Why", BoltCore.Icons.inspectorWindow?[IconSize.Small]);
        }

        public GraphCore.IGraphContext context { get; }

        public object sidebarControlHint => typeof(BehaviorTreeWhyPanel);

        public GUIContent titleContent { get; }

        public Vector2 minSize => new(300.0f, 200.0f);

        public void OnGUI(Rect position)
        {
            EnsureStyles();

            var y = position.y + Padding;
            var width = position.width - Padding * 2.0f;
            var x = position.x + Padding;

            y = DrawSourceControls(x, y, width);

            var recording = CurrentRecording();

            if (recording == null)
            {
                DrawHelp(x, ref y, width,
                    Application.isPlaying
                        ? "Select the agent in the hierarchy, or open its tree from the machine. Nothing here says which one to explain."
                        : "Enter play mode to watch an agent, or load an exported recording.");
                return;
            }

            var node = SelectedNode();

            if (node == null)
            {
                DrawHelp(x, ref y, width, "Select a node on the canvas to see why it did what it did.");
                return;
            }

            y = DrawCallSitePicker(x, y, width, recording, node.guid);

            var explanation = Explanation(recording, node.guid);

            if (explanation == null)
            {
                DrawHelp(x, ref y, width, "This node has no recorded activity in the selected call site.");
                return;
            }

            DrawExplanation(x, ref y, width, explanation);
        }

        public float GetHeight(float width)
        {
            EnsureStyles();

            var inner = width - Padding * 2.0f;
            var height = Padding * 2.0f + LineHeight() * 2.0f + RowSpacing * 2.0f;

            var recording = CurrentRecording();
            var node = SelectedNode();

            if (recording == null || node == null) return height + HelpHeight(inner);

            var explanation = Explanation(recording, node.guid);
            if (explanation == null) return height + HelpHeight(inner);

            height += LineHeight() + RowSpacing;
            height += wrapped.CalcHeight(new GUIContent(explanation.CallSitePath), inner) + RowSpacing;
            height += headline.CalcHeight(new GUIContent(explanation.Headline), inner) + RowSpacing;

            foreach (var clause in explanation.Clauses)
            {
                height += ClauseHeight(clause, inner) + RowSpacing;
            }

            if (explanation.Trace != null && explanation.Trace.Chain.Count > 1)
            {
                height += LineHeight() * (explanation.Trace.Chain.Count + 1) + RowSpacing;
            }

            return height;
        }

        #region Sources

        /// <summary>
        /// The agent being explained, and where that answer came from.
        ///
        /// <para>
        /// Deliberately not a picker. The canvas is already showing one particular agent's tree instance, so
        /// a second control choosing a different agent would not merely be confusing — clicking a node would
        /// explain <em>another</em> agent's history for that guid, which is a wrong answer rather than an
        /// awkward one. There is one source of truth and this reads it.
        /// </para>
        /// </summary>
        private BehaviorTreeMachine CurrentMachine(out string source)
        {
            // The canvas's own reference knows which machine it was opened through. This is the answer
            // whenever the tree is being watched live, which is the case the panel exists for.
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

            // The tree was opened as a bare asset, so the canvas has no agent. Hierarchy selection is then
            // the only statement of intent the user has made.
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
            // refusing to would make the panel useless in the common single-enemy case.
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
        /// cheaper answers failed, which is why the registry is not consulted — it holds recordings rather
        /// than agents, and a topology needs the machine.
        /// </summary>
        private static BehaviorTreeMachine SingleRecordingMachine()
        {
            if (!Application.isPlaying) return null;

            BehaviorTreeMachine found = null;

            foreach (var machine in UnityEngine.Object.FindObjectsByType<BehaviorTreeMachine>(FindObjectsSortMode.None))
            {
                if (machine == null || machine.FlightRecorder == null) continue;

                if (found != null) return null;

                found = machine;
            }

            return found;
        }

        private IBehaviorTreeRecording CurrentRecording()
        {
            if (loaded != null) return loaded;

            return CurrentMachine(out _)?.FlightRecorder;
        }

        private IBehaviorTreeTopology CurrentTopology()
        {
            // A loaded recording has no live graph, so it falls back to whatever tree is open on the canvas.
            // That is right more often than it sounds: the reason someone opened the recording is usually that
            // they are looking at the tree it came from. When it is wrong, names simply do not resolve and the
            // explanation degrades to guids rather than lying.
            if (loaded == null)
            {
                var machine = CurrentMachine(out _);
                if (machine != null) return BehaviorTreeGraphTopology.From(machine);
            }

            return context?.graph is BehaviorTreeGraph graph ? BehaviorTreeGraphTopology.From(graph) : null;
        }

        private BehaviorTreeNode SelectedNode()
        {
            if (context == null) return null;

            foreach (var element in context.selection)
            {
                if (element is BehaviorTreeNode node && node.IsVisible) return node;
            }

            return null;
        }

        private float DrawSourceControls(float x, float y, float width)
        {
            var row = new Rect(x, y, width, LineHeight());

            if (loaded != null)
            {
                var labelWidth = width - 60.0f;
                EditorGUI.LabelField(new Rect(x, y, labelWidth, row.height), $"File: {loadedFrom}", EditorStyles.miniLabel);

                if (GUI.Button(new Rect(x + labelWidth, y, 60.0f, row.height), "Close", EditorStyles.miniButton))
                {
                    loaded = null;
                    loadedFrom = null;
                    Invalidate();
                }
            }
            else
            {
                // A label rather than a control. Saying which agent this is, and why it is that one, is what
                // a reader needs; letting them choose a different one is what would make the answer wrong.
                var machine = CurrentMachine(out var source);

                EditorGUI.LabelField(
                    row,
                    machine != null ? $"{machine.name}  ({source})" : "No live agent",
                    EditorStyles.miniLabel);
            }

            y += row.height + RowSpacing;

            var buttons = new Rect(x, y, width, LineHeight());
            var half = width / 2.0f - 2.0f;

            using (new EditorGUI.DisabledScope(CurrentRecording() == null || loaded != null))
            {
                if (GUI.Button(new Rect(buttons.x, buttons.y, half, buttons.height), "Export…", EditorStyles.miniButton))
                {
                    Export();
                }
            }

            if (GUI.Button(new Rect(buttons.x + half + 4.0f, buttons.y, half, buttons.height), "Load…", EditorStyles.miniButton))
            {
                Load();
            }

            return y + buttons.height + RowSpacing;
        }

        private float DrawCallSitePicker(float x, float y, float width, IBehaviorTreeRecording recording, Guid nodeGuid)
        {
            var callSites = BehaviorTreeExplainer.CallSitesFor(recording, nodeGuid);
            if (callSites.Count <= 1) return y;

            // One branch asset used twice is two different stories, and the canvas cannot say which one the
            // designer meant — the clone keeps the original guids.
            var names = new string[callSites.Count];
            for (int i = 0; i < callSites.Count; i++)
            {
                names[i] = BehaviorTreeExplainer.CallSitePath(recording, callSites[i]);
            }

            if (callSiteIndex >= names.Length) callSiteIndex = 0;

            var row = new Rect(x, y, width, LineHeight());
            var picked = EditorGUI.Popup(row, callSiteIndex, names);

            if (picked != callSiteIndex)
            {
                callSiteIndex = picked;
                Invalidate();
            }

            return y + row.height + RowSpacing;
        }

        private int CurrentScope(IBehaviorTreeRecording recording, Guid nodeGuid)
        {
            var callSites = BehaviorTreeExplainer.CallSitesFor(recording, nodeGuid);
            if (callSites.Count == 0) return BehaviorTreeCallSite.RootId;

            return callSites[Mathf.Clamp(callSiteIndex, 0, callSites.Count - 1)];
        }

        #endregion

        #region Drawing

        private void DrawExplanation(float x, ref float y, float width, BehaviorTreeExplanation explanation)
        {
            EditorGUI.LabelField(new Rect(x, y, width, LineHeight()), explanation.SubjectName, EditorStyles.boldLabel);
            y += LineHeight() + RowSpacing;

            var pathHeight = wrapped.CalcHeight(new GUIContent(explanation.CallSitePath), width);
            EditorGUI.LabelField(new Rect(x, y, width, pathHeight), explanation.CallSitePath, EditorStyles.miniLabel);
            y += pathHeight + RowSpacing;

            var headlineHeight = headline.CalcHeight(new GUIContent(explanation.Headline), width);
            EditorGUI.LabelField(new Rect(x, y, width, headlineHeight), explanation.Headline, headline);
            y += headlineHeight + RowSpacing;

            foreach (var clause in explanation.Clauses)
            {
                DrawClause(x, ref y, width, clause);
            }

            DrawChain(x, ref y, width, explanation.Trace);
        }

        /// <summary>
        /// The chain the guard was reading, indented as a tree.
        ///
        /// <para>
        /// This is the part that turns "it returned false" into an answer. Most branches are guarded by a
        /// Visual Scripting graph, so without this the reader is told the guard was false and left to open
        /// the graph and work out which of its inputs did it.
        /// </para>
        /// </summary>
        private void DrawChain(float x, ref float y, float width, GuardTrace trace)
        {
            if (trace == null || trace.Chain.Count <= 1) return;

            EditorGUI.LabelField(new Rect(x, y, width, LineHeight()), "GUARD CHAIN", role);
            y += LineHeight();

            // From index 1: the headline already named the guard and said what it returned, and the chain's
            // own record of the guard's name is its raw node name rather than the one the sentence uses.
            for (int i = 1; i < trace.Chain.Count; i++)
            {
                var node = trace.Chain[i];
                var indent = (node.Depth - 1) * 12.0f;
                var row = new Rect(x + indent, y, width - indent, LineHeight());

                var canFollow = FindOnCanvas(node.NodeGuid) != null;
                var buttons = (canFollow ? LinkButtonWidth : 0.0f) + (node.HasSnapshot ? SnapshotButtonWidth : 0.0f);
                var textWidth = Mathf.Max(40.0f, row.width - buttons - 4.0f);

                EditorGUI.LabelField(
                    new Rect(row.x, row.y, textWidth, row.height),
                    $"{(node.Depth <= 1 ? string.Empty : "└ ")}{node.Name}  →  {node.Value}",
                    node.Depth == 1 ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);

                var cursor = row.x + textWidth + 4.0f;

                if (node.HasSnapshot)
                {
                    if (GUI.Button(new Rect(cursor, row.y, SnapshotButtonWidth, row.height), "graph", EditorStyles.miniButton))
                    {
                        OpenSnapshot(trace, node);
                    }

                    cursor += SnapshotButtonWidth;
                }

                if (canFollow && GUI.Button(new Rect(cursor, row.y, LinkButtonWidth, row.height), "→", EditorStyles.miniButton))
                {
                    Follow(BehaviorTreeExplanationLink.ToNode(node.NodeGuid, trace.CallSiteId));
                }

                y += LineHeight();
            }

            y += RowSpacing;
        }

        /// <summary>
        /// Opens the snapshot for a chain node, resolving the live asset so the window can lay the graph out.
        /// A recording loaded from a file usually has no asset to resolve, and the window falls back to a wire
        /// list rather than refusing.
        /// </summary>
        private void OpenSnapshot(GuardTrace trace, GuardTraceNode node)
        {
            if (node.SnapshotIndex < 0 || node.SnapshotIndex >= trace.Snapshots.Count) return;

            var snapshot = trace.Snapshots[node.SnapshotIndex];

            BehaviorTreeGuardSnapshotWindow.Open(trace, snapshot, ResolveGraphAsset(snapshot), node.Name);
        }

        private ScriptGraphAsset ResolveGraphAsset(GuardGraphSnapshot snapshot)
        {
            var owner = FindOnCanvas(snapshot.OwnerNodeGuid);
            if (owner?.scriptGraphAssets == null) return null;

            ScriptGraphAsset first = null;

            foreach (var asset in owner.scriptGraphAssets)
            {
                if (asset == null) continue;

                first ??= asset;

                // Names are not unique across a project, so this is a preference rather than an identity
                // check — matching the recorded name picks the right one when a node owns several.
                if (asset.name == snapshot.GraphName) return asset;
            }

            return first;
        }

        private void DrawClause(float x, ref float y, float width, BehaviorTreeExplanationClause clause)
        {
            var canFollow = CanFollow(clause.Link);
            var textWidth = canFollow ? width - LinkButtonWidth - 2.0f : width;
            var height = ClauseHeight(clause, width);

            var tag = new Rect(x, y, 60.0f, LineHeight());
            EditorGUI.LabelField(tag, RoleLabel(clause.Role), role);

            var body = new Rect(x, y + LineHeight(), textWidth, height - LineHeight());
            EditorGUI.LabelField(body, clause.Text, wrapped);

            if (canFollow)
            {
                var button = new Rect(x + textWidth + 2.0f, y + LineHeight(), LinkButtonWidth, LineHeight());

                if (GUI.Button(button, LinkGlyph(clause.Link), EditorStyles.miniButton)) Follow(clause.Link);
            }

            y += height + RowSpacing;
        }

        private void DrawHelp(float x, ref float y, float width, string message)
        {
            var height = LudiqGUIUtility.GetHelpBoxHeight(message, MessageType.Info, width);
            EditorGUI.HelpBox(new Rect(x, y, width, height), message, MessageType.Info);
            y += height + RowSpacing;
        }

        private float ClauseHeight(BehaviorTreeExplanationClause clause, float width)
        {
            var textWidth = CanFollow(clause.Link) ? width - LinkButtonWidth - 2.0f : width;

            return LineHeight() + wrapped.CalcHeight(new GUIContent(clause.Text), textWidth);
        }

        private float HelpHeight(float width) => EditorGUIUtility.singleLineHeight * 3.0f;

        private static float LineHeight() => EditorGUIUtility.singleLineHeight;

        private static string RoleLabel(BehaviorTreeClauseRole clauseRole)
        {
            return clauseRole switch
            {
                BehaviorTreeClauseRole.Cause => "BECAUSE",
                BehaviorTreeClauseRole.Evidence => "EVIDENCE",
                BehaviorTreeClauseRole.Context => "CONTEXT",
                _ => "CAVEAT",
            };
        }

        private void EnsureStyles()
        {
            if (wrapped != null) return;

            wrapped = new GUIStyle(EditorStyles.label) { wordWrap = true };
            headline = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
            role = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
        }

        #endregion

        #region Links

        /// <summary>
        /// Whether following a link would actually go somewhere. A node inside a sub-tree instance is not on
        /// the open canvas, and a button that silently does nothing is worse than no button.
        /// </summary>
        private bool CanFollow(BehaviorTreeExplanationLink link)
        {
            // A tick goes to the scrubber rather than to the canvas, so it needs no node to land on. This is
            // what the explainer's tick links were emitted for; until Component 2 they went nowhere.
            if (link.Kind == BehaviorTreeLinkKind.Tick) return link.Tick >= 0;

            if (link.Kind != BehaviorTreeLinkKind.Node && link.Kind != BehaviorTreeLinkKind.Guard) return false;

            return FindOnCanvas(link.NodeGuid) != null;
        }

        private void Follow(BehaviorTreeExplanationLink link)
        {
            if (link.Kind == BehaviorTreeLinkKind.Tick)
            {
                BehaviorTreeTimelinePanel.RequestScrub(link.Tick);
                return;
            }

            var node = FindOnCanvas(link.NodeGuid);
            if (node == null) return;

            context.selection.Select(node);
            Invalidate();
        }

        /// <summary>The glyph for a link, so a jump-in-time does not look like a jump-on-canvas.</summary>
        private static string LinkGlyph(BehaviorTreeExplanationLink link)
        {
            return link.Kind == BehaviorTreeLinkKind.Tick ? "⏱" : "→";
        }

        private BehaviorTreeNode FindOnCanvas(Guid guid)
        {
            if (context?.graph is not BehaviorTreeGraph graph) return null;

            foreach (var node in graph.Nodes)
            {
                if (node != null && node.guid == guid) return node;
            }

            return null;
        }

        #endregion

        #region Explanation cache

        /// <summary>
        /// The explanation for the current selection, recomputed only when something it depends on moved.
        /// IMGUI asks for a height and then draws, so an uncached explainer would run twice per repaint of a
        /// panel that is open all session.
        /// </summary>
        private BehaviorTreeExplanation Explanation(IBehaviorTreeRecording recording, Guid nodeGuid)
        {
            var scope = CurrentScope(recording, nodeGuid);

            // The recording itself is part of the key: selecting a different agent in the hierarchy changes
            // which one this panel is about, and a cache watching only tick and count would happily serve the
            // previous agent's answer for the new one.
            if (cached != null &&
                ReferenceEquals(cachedRecording, recording) &&
                cachedNode == nodeGuid &&
                cachedScope == scope &&
                cachedTick == recording.Tick &&
                cachedEventCount == recording.EventCount)
            {
                return cached;
            }

            cached = BehaviorTreeExplainer.Explain(recording, scope, nodeGuid, CurrentTopology());
            cachedRecording = recording;
            cachedNode = nodeGuid;
            cachedScope = scope;
            cachedTick = recording.Tick;
            cachedEventCount = recording.EventCount;

            return cached;
        }

        private void Invalidate()
        {
            cached = null;
            cachedRecording = null;
            cachedTick = -1;
            cachedEventCount = -1;
        }

        #endregion

        #region Files

        private void Export()
        {
            if (CurrentRecording() is not BehaviorTreeFlightRecorder recorder) return;

            var path = EditorUtility.SaveFilePanel(
                "Export recording", "", $"{recorder.AgentName}-recording.json", "json");

            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, BehaviorTreeRecordingDump.ToJson(recorder));
        }

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
            callSiteIndex = 0;
            Invalidate();
        }

        #endregion
    }
}

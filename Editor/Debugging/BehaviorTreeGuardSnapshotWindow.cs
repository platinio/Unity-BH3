using System;
using System.Collections.Generic;
using ArcaneOnyx.BehaviorTree.Debugging;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A Visual Scripting guard graph as it was at the instant a guard changed its mind.
    ///
    /// <para>
    /// Draws its own canvas rather than writing the recorded values back into the graph's live debug data and
    /// letting Unity's renderer show them. That shortcut was tempting and is wrong twice over: the same debug
    /// data is what the next capture reads, so displaying a snapshot would corrupt the following one, and the
    /// values would keep showing on the real canvas after this window closed — looking exactly like live
    /// values, which is the one thing a snapshot must never do. Everything here is deliberately ghosted and
    /// banner-marked so the two cannot be confused.
    /// </para>
    ///
    /// <para>
    /// The snapshot holds values and guids, not a copy of the graph, so the layout comes from the asset as it
    /// is now. Edit the graph after recording and some wires will no longer exist; those are listed as
    /// orphaned rather than dropped, because a partially readable account of a bug that already happened
    /// still beats none.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeGuardSnapshotWindow : EditorWindow
    {
        private const float UnitWidth = 170.0f;
        private const float UnitHeight = 46.0f;
        private const float BannerHeight = 40.0f;

        private static readonly Color Ghost = new(0.55f, 0.58f, 0.62f, 1.0f);
        private static readonly Color Evaluated = new(0.42f, 0.72f, 0.95f, 1.0f);
        private static readonly Color NotEvaluated = new(0.42f, 0.42f, 0.46f, 1.0f);

        private GuardGraphSnapshot snapshot;
        private ScriptGraphAsset asset;
        private int tick;
        private int sequence;
        private bool result;
        private string guardName;

        private Vector2 pan;
        private float zoom = 1.0f;
        private Vector2 listScroll;

        /// <summary>
        /// Opens a snapshot. <paramref name="asset"/> may be null — for an imported recording with no project
        /// asset to lay out against, the window falls back to a wire list, which carries the same values
        /// without the geometry.
        /// </summary>
        public static void Open(GuardTrace trace, GuardGraphSnapshot snapshot, ScriptGraphAsset asset, string guardName)
        {
            if (trace == null || snapshot == null) return;

            var window = GetWindow<BehaviorTreeGuardSnapshotWindow>(utility: false, title: "Guard Snapshot", focus: true);

            window.snapshot = snapshot;
            window.asset = asset;
            window.tick = trace.Tick;
            window.sequence = trace.Sequence;
            window.result = trace.Result;
            window.guardName = guardName;
            window.pan = Vector2.zero;
            window.zoom = 1.0f;

            window.Repaint();
        }

        private void OnGUI()
        {
            if (snapshot == null)
            {
                EditorGUILayout.HelpBox("No snapshot. Open one from the Why panel's guard chain.", MessageType.Info);
                return;
            }

            DrawBanner();

            var canvas = new Rect(0.0f, BannerHeight, position.width, position.height - BannerHeight);

            if (asset?.graph == null)
            {
                DrawWireList(canvas, "No graph asset to lay out against, so the wires are listed instead.");
                return;
            }

            HandleNavigation(canvas);
            DrawCanvas(canvas);
        }

        private void DrawBanner()
        {
            var banner = new Rect(0.0f, 0.0f, position.width, BannerHeight);

            // Loud on purpose, and always visible. A snapshot that reads as live is worse than no snapshot,
            // because it invites a conclusion about the present drawn from the past.
            EditorGUI.DrawRect(banner, new Color(0.45f, 0.32f, 0.05f, 1.0f));

            var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            var detail = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };

            EditorGUI.LabelField(
                new Rect(8.0f, 2.0f, position.width - 16.0f, 18.0f),
                $"SNAPSHOT — not live values", style);

            EditorGUI.LabelField(
                new Rect(8.0f, 20.0f, position.width - 16.0f, 16.0f),
                $"'{guardName}' returned {(result ? "true" : "false")} at tick {tick} (step {sequence})   ·   graph '{snapshot.GraphName}'",
                detail);
        }

        private void HandleNavigation(Rect canvas)
        {
            var e = Event.current;
            if (!canvas.Contains(e.mousePosition)) return;

            if (e.type == EventType.ScrollWheel)
            {
                zoom = Mathf.Clamp(zoom - e.delta.y * 0.03f, 0.35f, 2.0f);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && (e.button == 0 || e.button == 2))
            {
                pan += e.delta;
                e.Use();
                Repaint();
            }
        }

        private void DrawCanvas(Rect canvas)
        {
            GUI.BeginClip(canvas);

            var inner = new Rect(0.0f, 0.0f, canvas.width, canvas.height);
            EditorGUI.DrawRect(inner, new Color(0.16f, 0.16f, 0.17f, 1.0f));

            var units = new Dictionary<Guid, IUnit>();
            foreach (var unit in asset.graph.units)
            {
                if (unit != null) units[unit.guid] = unit;
            }

            var origin = Centre(units, canvas);
            var orphans = new List<GuardWireValue>();

            foreach (var wire in snapshot.Wires)
            {
                if (!units.TryGetValue(wire.SourceUnitGuid, out var source) ||
                    !units.TryGetValue(wire.DestinationUnitGuid, out var destination))
                {
                    // The asset moved on since this was recorded. Saying so is better than drawing a line to
                    // a unit that is not the one the value came from.
                    orphans.Add(wire);
                    continue;
                }

                DrawWire(Place(source, origin), Place(destination, origin), wire);
            }

            foreach (var unit in units.Values)
            {
                DrawUnit(Place(unit, origin), unit);
            }

            GUI.EndClip();

            if (orphans.Count > 0) DrawOrphans(canvas, orphans);
        }

        private Vector2 Centre(Dictionary<Guid, IUnit> units, Rect canvas)
        {
            if (units.Count == 0) return new Vector2(canvas.width * 0.5f, canvas.height * 0.5f);

            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);

            foreach (var unit in units.Values)
            {
                min = Vector2.Min(min, unit.position);
                max = Vector2.Max(max, unit.position);
            }

            var middle = (min + max) * 0.5f;

            return new Vector2(canvas.width * 0.5f, canvas.height * 0.5f) - middle * zoom + pan;
        }

        private Rect Place(IUnit unit, Vector2 origin)
        {
            return new Rect(
                origin.x + unit.position.x * zoom,
                origin.y + unit.position.y * zoom,
                UnitWidth * zoom,
                UnitHeight * zoom);
        }

        private void DrawUnit(Rect rect, IUnit unit)
        {
            EditorGUI.DrawRect(rect, new Color(0.24f, 0.25f, 0.27f, 1.0f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2.0f * zoom), Ghost);

            if (zoom < 0.55f) return;

            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = Ghost },
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };

            EditorGUI.LabelField(rect, unit.GetType().Name, style);
        }

        private void DrawWire(Rect from, Rect to, GuardWireValue wire)
        {
            var start = new Vector2(from.xMax, from.center.y);
            var end = new Vector2(to.xMin, to.center.y);
            var colour = wire.WasEvaluated ? Evaluated : NotEvaluated;

            Handles.BeginGUI();
            Handles.color = colour;

            var tangent = Mathf.Max(30.0f, Mathf.Abs(end.x - start.x) * 0.4f);
            Handles.DrawBezier(
                start, end,
                start + Vector2.right * tangent,
                end + Vector2.left * tangent,
                colour, null, wire.WasEvaluated ? 2.5f : 1.5f);

            Handles.EndGUI();

            if (zoom < 0.5f) return;

            var label = wire.Label;
            var mid = (start + end) * 0.5f;
            var size = EditorStyles.miniLabel.CalcSize(new GUIContent(label));
            var box = new Rect(mid.x - size.x * 0.5f - 3.0f, mid.y - size.y * 0.5f - 1.0f, size.x + 6.0f, size.y + 2.0f);

            EditorGUI.DrawRect(box, new Color(0.1f, 0.1f, 0.11f, 0.92f));

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = colour },
                alignment = TextAnchor.MiddleCenter,
            };

            EditorGUI.LabelField(box, label, style);
        }

        private void DrawOrphans(Rect canvas, List<GuardWireValue> orphans)
        {
            var height = Mathf.Min(canvas.height * 0.4f, 24.0f + orphans.Count * 16.0f);
            var panel = new Rect(canvas.x, canvas.yMax - height, canvas.width, height);

            EditorGUI.DrawRect(panel, new Color(0.28f, 0.16f, 0.16f, 0.96f));

            EditorGUI.LabelField(
                new Rect(panel.x + 6.0f, panel.y + 3.0f, panel.width - 12.0f, 16.0f),
                $"{orphans.Count} recorded wire(s) no longer exist in this graph — it was edited after the recording.",
                new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(1.0f, 0.8f, 0.8f) } });

            var list = new Rect(panel.x + 6.0f, panel.y + 21.0f, panel.width - 12.0f, panel.height - 24.0f);
            listScroll = GUI.BeginScrollView(list, listScroll, new Rect(0.0f, 0.0f, list.width - 16.0f, orphans.Count * 16.0f));

            for (int i = 0; i < orphans.Count; i++)
            {
                EditorGUI.LabelField(
                    new Rect(0.0f, i * 16.0f, list.width - 16.0f, 16.0f),
                    $"{Short(orphans[i].SourceUnitGuid)}.{orphans[i].SourceKey} → {Short(orphans[i].DestinationUnitGuid)}.{orphans[i].DestinationKey} = {orphans[i].Label}",
                    EditorStyles.miniLabel);
            }

            GUI.EndScrollView();
        }

        private void DrawWireList(Rect canvas, string note)
        {
            EditorGUI.LabelField(new Rect(canvas.x + 6.0f, canvas.y + 4.0f, canvas.width - 12.0f, 16.0f), note, EditorStyles.miniLabel);

            var list = new Rect(canvas.x + 6.0f, canvas.y + 22.0f, canvas.width - 12.0f, canvas.height - 26.0f);
            var content = new Rect(0.0f, 0.0f, list.width - 16.0f, snapshot.Wires.Count * 18.0f);

            listScroll = GUI.BeginScrollView(list, listScroll, content);

            for (int i = 0; i < snapshot.Wires.Count; i++)
            {
                var wire = snapshot.Wires[i];
                var style = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = wire.WasEvaluated ? Evaluated : NotEvaluated },
                };

                EditorGUI.LabelField(
                    new Rect(0.0f, i * 18.0f, content.width, 18.0f),
                    $"{Short(wire.SourceUnitGuid)}.{wire.SourceKey} → {Short(wire.DestinationUnitGuid)}.{wire.DestinationKey} = {wire.Label}",
                    style);
            }

            GUI.EndScrollView();
        }

        private static string Short(Guid guid) => guid == Guid.Empty ? "?" : guid.ToString("N").Substring(0, 6);
    }
}

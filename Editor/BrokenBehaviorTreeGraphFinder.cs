using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Scans BehaviorTreeGraphAsset files for broken VS units (invalid member
    /// references, unresolvable types, broken connections).
    ///
    /// BehaviorTreeGraphAsset uses LudiqScriptableObject serialization and stores
    /// a graph whose elements are IUnit instances — the same infrastructure used by
    /// FlowGraph — but its C# type is not ScriptGraphAsset, so the standard
    /// BrokenScriptGraphFinder never returns these assets.
    /// </summary>
    public class BrokenBehaviorTreeGraphFinder : EditorWindow
    {
        private class GraphIssue
        {
            public string label;
            public string assetPath;
            public UnityEngine.Object asset;
            public List<string> issues = new List<string>();
        }

        // Member.Reflect() — bypasses the isReflected cache so we always get a
        // fresh result even when the stored signature is outdated.
        private static readonly MethodInfo MemberReflectMethod =
            typeof(Member).GetMethod("Reflect",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private List<GraphIssue> _results = new List<GraphIssue>();
        private HashSet<string> _visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Vector2 _scroll;
        private bool _showClean;
        private string _typeStatus = string.Empty;

        [MenuItem("Tools/Visual Scripting/Find Broken Behavior Tree Graphs")]
        public static void Open()
        {
            var w = GetWindow<BrokenBehaviorTreeGraphFinder>("Broken BT Graphs");
            w.minSize = new Vector2(520, 380);
            w.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawBody();
        }

        // ──────────────────────────────────────────────────────────────────────
        // UI
        // ──────────────────────────────────────────────────────────────────────

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Scan All", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Scan();
                if (_results.Count > 0 &&
                    GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    _results.Clear();
                    Repaint();
                }
                GUILayout.FlexibleSpace();
                _showClean = GUILayout.Toggle(_showClean, "Show Clean", EditorStyles.toolbarButton);
            }
        }

        private void DrawBody()
        {
            if (!string.IsNullOrEmpty(_typeStatus))
                EditorGUILayout.HelpBox(_typeStatus, MessageType.Info);

            if (_results.Count == 0)
            {
                if (string.IsNullOrEmpty(_typeStatus))
                    EditorGUILayout.HelpBox(
                        "Scans all BehaviorTreeGraphAsset files for broken VS units " +
                        "(invalid member references, missing types, broken connections).",
                        MessageType.Info);
                return;
            }

            int broken = _results.Count(r => r.issues.Count > 0);
            string msg = broken == 0
                ? $"All {_results.Count} graphs look clean."
                : $"{broken} broken graph(s) out of {_results.Count} scanned.";
            EditorGUILayout.HelpBox(msg, broken == 0 ? MessageType.Info : MessageType.Warning);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var r in _results)
            {
                if (!_showClean && r.issues.Count == 0) continue;
                DrawResult(r);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawResult(GraphIssue r)
        {
            bool broken = r.issues.Count > 0;
            Color prev = GUI.backgroundColor;
            if (broken) GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.backgroundColor = prev;

                using (new EditorGUILayout.HorizontalScope())
                {
                    string icon = broken ? "console.erroricon.sml" : "console.infoicon.sml";
                    GUILayout.Label(EditorGUIUtility.IconContent(icon),
                        GUILayout.Width(20), GUILayout.Height(18));
                    EditorGUILayout.LabelField(r.label, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    if (r.asset != null)
                    {
                        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                            EditorGUIUtility.PingObject(r.asset);
                        if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(55)))
                            Selection.activeObject = r.asset;
                        if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(40)))
                        {
                            Selection.activeObject = r.asset;
                            AssetDatabase.OpenAsset(r.asset);
                        }
                    }
                }

                EditorGUILayout.LabelField(r.assetPath, EditorStyles.miniLabel);

                if (broken)
                {
                    Color prevContent = GUI.contentColor;
                    GUI.contentColor = new Color(1f, 0.65f, 0.65f);
                    foreach (var issue in r.issues)
                        EditorGUILayout.LabelField("  • " + issue, EditorStyles.wordWrappedMiniLabel);
                    GUI.contentColor = prevContent;
                }
            }

            GUI.backgroundColor = prev;
            EditorGUILayout.Space(2);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Scanning
        // ──────────────────────────────────────────────────────────────────────

        private void Scan()
        {
            _results.Clear();
            _visited.Clear();
            _typeStatus = string.Empty;

            Type btType = FindBehaviorTreeGraphAssetType();

            string[] guids;
            if (btType != null)
            {
                _typeStatus = $"Found type: {btType.FullName}";
                guids = AssetDatabase.FindAssets($"t:{btType.Name}");
            }
            else
            {
                // Type not directly accessible — fall back to a full asset scan
                // filtered by types that expose a graph with IUnit elements.
                _typeStatus = "BehaviorTreeGraphAsset type not directly resolvable — " +
                              "falling back to LudiqScriptableObject scan.";
                guids = AssetDatabase.FindAssets("t:ScriptableObject");
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar(
                    "Scanning Behavior Tree Graphs",
                    System.IO.Path.GetFileName(path),
                    (float)i / guids.Length)) break;

                try
                {
                    // When using the broad ScriptableObject fallback, skip types
                    // the standard BrokenScriptGraphFinder already covers.
                    if (btType == null)
                    {
                        var loaded = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                        if (loaded == null) continue;
                        if (loaded is ScriptGraphAsset || loaded is StateGraphAsset) continue;
                        if (!IsLikelyBehaviorTreeAsset(loaded)) continue;
                    }

                    ScanAsset(path, _visited);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    _results.Add(new GraphIssue
                    {
                        label = System.IO.Path.GetFileNameWithoutExtension(path),
                        assetPath = path,
                        issues = { $"Scan exception: {ex.Message}" }
                    });
                }
            }

            EditorUtility.ClearProgressBar();
            Repaint();
        }

        private void ScanAsset(string path, HashSet<string> visited)
        {
            if (!visited.Add(path)) return;

            // LoadAllAssetsAtPath returns the main asset AND every sub-asset packed
            // in the same file. Embedded ScriptGraphAssets live as sub-assets inside
            // the BehaviorTreeGraphAsset file (same GUID, different fileID), so a plain
            // LoadAssetAtPath only ever sees the first object and misses the rest.
            UnityEngine.Object[] all;
            try { all = AssetDatabase.LoadAllAssetsAtPath(path); }
            catch (Exception ex) { Debug.LogException(ex); return; }


            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

            foreach (var obj in all)
            {
                if (!(obj is ScriptableObject so)) continue;

                object graph;
                List<IUnit> units;
                try
                {
                    graph = GetGraphObject(so);
                    if (graph == null) continue;
                    units = GetGraphUnits(graph).ToList();
                    if (units.Count == 0) continue;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    continue;
                }

                // Label: "FileName" for the main asset, "FileName / SubAssetName" for sub-assets.
                string label = string.IsNullOrEmpty(so.name) || so.name == fileName
                    ? fileName
                    : $"{fileName} / {so.name}";

                var result = new GraphIssue
                {
                    label     = label,
                    assetPath = path,
                    asset     = so,
                };

                foreach (IUnit unit in units)
                {
                    try { CheckUnit(unit, result); }
                    catch (Exception ex)
                    {
                        string unitType = unit?.GetType().Name ?? "null";
                        result.issues.Add($"Unit check exception ({unitType}): {ex.Message}");
                        Debug.LogException(ex);
                    }
                }

                _results.Add(result);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Reflection helpers
        // ──────────────────────────────────────────────────────────────────────

        // Locate BehaviorTreeGraphAsset across all loaded assemblies by known names.
        private static Type FindBehaviorTreeGraphAssetType()
        {
            string[] candidateNames =
            {
                "ArcaneOnyx.BehaviorTree.BehaviorTreeGraphAsset",
                "ArcaneOnyx.BH3.BehaviorTreeGraphAsset",
            };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var name in candidateNames)
                {
                    var t = assembly.GetType(name);
                    if (t != null) return t;
                }
            }
            return null;
        }

        // True when the fallback scan encounters a LudiqScriptableObject subclass
        // that is NOT one of the standard VS asset types.
        private static bool IsLikelyBehaviorTreeAsset(ScriptableObject obj)
        {
            var t = obj.GetType();
            // Accept any LudiqScriptableObject that isn't already handled.
            while (t != null && t != typeof(ScriptableObject))
            {
                if (t.Name == "LudiqScriptableObject") return true;
                t = t.BaseType;
            }
            return false;
        }

        // Return the graph object held by the asset, via the "graph" property/field.
        private static object GetGraphObject(ScriptableObject asset)
        {
            var type = asset.GetType();
            var graphProp = type.GetProperty("graph",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (graphProp != null)
            {
                try { return graphProp.GetValue(asset); }
                catch { }
            }

            var graphField = type.GetField("graph",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (graphField != null)
            {
                try { return graphField.GetValue(asset); }
                catch { }
            }

            return null;
        }

        // Extract IUnit elements from any graph object by trying known collection names.
        private static IEnumerable<IUnit> GetGraphUnits(object graph)
        {
            if (graph is FlowGraph fg)
                return fg.units.OfType<IUnit>();

            var type = graph.GetType();
            foreach (string name in new[] { "units", "elements", "nodes" })
            {
                try
                {
                    var prop = type.GetProperty(name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop?.GetValue(graph) is IEnumerable seq)
                        return seq.OfType<IUnit>();
                }
                catch { }
            }

            return Enumerable.Empty<IUnit>();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Unit checking (same logic as BrokenScriptGraphFinder)
        // ──────────────────────────────────────────────────────────────────────

        private static void CheckUnit(IUnit unit, GraphIssue result)
        {
            if (unit == null) { result.issues.Add("Null unit reference."); return; }

            string typeName = unit.GetType().Name;

            if (typeName == "InvalidUnit")
            {
                result.issues.Add("Unresolvable unit — " + GetInvalidUnitDescription(unit));
                return;
            }

            if (unit is MemberUnit memberUnit)
            {
                Member member = memberUnit.member;
                if (member == null)
                {
                    result.issues.Add($"{typeName}: member is null.");
                }
                else
                {
                    // Pre-compute before Reflect() — member.targetType may be null,
                    // causing ToUniqueString() itself to throw inside a catch block.
                    string memberStr;
                    try { memberStr = member.ToUniqueString(); }
                    catch { memberStr = $"{member.targetTypeName}.{member.name}"; }

                    try
                    {
                        if (MemberReflectMethod != null)
                            MemberReflectMethod.Invoke(member, null);
                        else
                            member.EnsureReflected();

                        if (!member.isReflected)
                            result.issues.Add($"{typeName}: unresolved member '{memberStr}'");
                    }
                    catch (TargetInvocationException tie)
                    {
                        result.issues.Add(
                            $"{typeName} '{memberStr}': " +
                            (tie.InnerException?.Message ?? tie.Message));
                    }
                    catch (Exception ex)
                    {
                        result.issues.Add($"{typeName} '{memberStr}': {ex.Message}");
                    }
                }
            }

            try
            {
                foreach (IUnitPort port in unit.ports)
                {
                    try
                    {
                        foreach (IUnitConnection conn in port.connections)
                        {
                            if (conn == null)
                            {
                                result.issues.Add($"{typeName}: null connection on '{port.key}'.");
                                continue;
                            }
                            try { _ = conn.source; _ = conn.destination; }
                            catch (Exception ex)
                            {
                                result.issues.Add(
                                    $"{typeName}: broken connection on '{port.key}' — {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.issues.Add($"{typeName}: port '{port.key}' error — {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.issues.Add($"{typeName}: port enumeration error — {ex.Message}");
            }
        }

        private static string GetInvalidUnitDescription(IUnit unit)
        {
            var type = unit.GetType();
            foreach (string name in new[]
                { "deserializationFailureReason", "invalidType", "data", "_originalData" })
            {
                try
                {
                    var f = type.GetField(name,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (f == null) continue;
                    object val = f.GetValue(unit);
                    if (val is string s && !string.IsNullOrEmpty(s)) return s;
                    if (val != null) return val.ToString();
                }
                catch { }
            }
            try { return unit.ToString() ?? "unknown"; } catch { return "unknown"; }
        }
    }
}

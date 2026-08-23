using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Where a missing node's replacement is chosen: the candidate types, what picking one would keep and
    /// drop, and how widely to apply it.
    ///
    /// <para>
    /// One window rather than one picker per door. The inspector, the canvas context menu and the broken-tree
    /// finder all open this, so there is a single place where the choice is made and a single preview that
    /// can be trusted to agree with what Apply does.
    /// </para>
    /// </summary>
    public class MissingTypeRetargetWindow : EditorWindow
    {
        private MissingType placeholder;
        private BehaviorTreeGraphAsset asset;
        private string formerType;

        private Type selectedTarget;
        private MissingTypeRetarget.Preview preview;
        private Vector2 scroll;

        private readonly AdvancedDropdownState dropdownState = new AdvancedDropdownState();

        /// <summary>Opens the window for one placeholder in the open canvas.</summary>
        public static void Open(MissingType placeholder)
        {
            Open(placeholder, BehaviorTreeCanvas.GetBehaviorTreeGraphAsset());
        }

        /// <summary>Opens the window for one placeholder in a named tree.</summary>
        public static void Open(MissingType placeholder, BehaviorTreeGraphAsset asset)
        {
            var window = GetWindow<MissingTypeRetargetWindow>(true, "Replace Missing Node Type");
            window.minSize = new Vector2(460.0f, 300.0f);

            window.placeholder = placeholder;
            window.asset = asset;
            window.formerType = placeholder?.formerType;
            window.selectedTarget = null;
            window.preview = null;

            window.ShowUtility();
        }

        /// <summary>
        /// Opens the window for a former type with no particular node in hand — the finder's entry point,
        /// where the whole point is that the same rename is spread across trees nobody has open.
        /// </summary>
        public static void OpenForType(string formerType)
        {
            var window = GetWindow<MissingTypeRetargetWindow>(true, "Replace Missing Node Type");
            window.minSize = new Vector2(460.0f, 300.0f);

            window.placeholder = FindAnyPlaceholder(formerType, out var owningAsset);
            window.asset = owningAsset;
            window.formerType = formerType;
            window.selectedTarget = null;
            window.preview = null;

            window.ShowUtility();
        }

        /// <summary>
        /// Any one placeholder for the type, used as the specimen the preview is computed against. They all
        /// came from the same type, so what carries over is the same for each; the specimen only has to be
        /// representative, not chosen.
        /// </summary>
        private static MissingType FindAnyPlaceholder(string formerType, out BehaviorTreeGraphAsset owner)
        {
            owner = null;

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(BehaviorTreeGraphAsset)))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate == null || candidate.graph == null) continue;

                var placeholder = candidate.graph.Nodes.OfType<MissingType>()
                    .FirstOrDefault(node => node.formerType == formerType);

                if (placeholder == null) continue;

                owner = candidate;
                return placeholder;
            }

            return null;
        }

        private void OnGUI()
        {
            if (placeholder == null)
            {
                EditorGUILayout.HelpBox(
                    "This placeholder is gone — it was replaced, deleted, or its tree was reloaded.",
                    MessageType.Info);

                if (GUILayout.Button("Close")) Close();
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Former type", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(formerType) ? "(unknown)" : formerType,
                EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space();
            DrawPicker();
            EditorGUILayout.Space();
            DrawPreview();
            EditorGUILayout.Space();
            DrawApply();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPicker()
        {
            EditorGUILayout.LabelField("Replace with", EditorStyles.boldLabel);

            string caption = selectedTarget == null ? "Pick a replacement type…" : selectedTarget.FullName;
            var buttonRect = GUILayoutUtility.GetRect(new GUIContent(caption), EditorStyles.popup);

            if (!GUI.Button(buttonRect, caption, EditorStyles.popup)) return;

            var dropdown = new NodeTypeDropdown(
                dropdownState,
                MissingTypeRetarget.CandidateTypes(placeholder),
                placeholder.ShortFormerTypeName,
                type =>
                {
                    selectedTarget = type;
                    preview = MissingTypeRetarget.PreviewRetarget(placeholder, type);
                    Repaint();
                });

            dropdown.Show(buttonRect);
        }

        private void DrawPreview()
        {
            if (preview == null)
            {
                EditorGUILayout.HelpBox("Pick a type to see what carries over.", MessageType.None);
                return;
            }

            if (!preview.CanApply)
            {
                EditorGUILayout.HelpBox(preview.Failure, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("What this would do", EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                $"Keeps {preview.KeptMembers.Count} value(s) and {preview.KeptConnections.Count} connection(s).");

            if (preview.KeptMembers.Count > 0)
            {
                EditorGUILayout.LabelField("   kept: " + string.Join(", ", preview.KeptMembers), EditorStyles.miniLabel);
            }

            if (preview.DroppedMembers.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Dropped, because the replacement has nowhere to put them: "
                    + string.Join(", ", preview.DroppedMembers), MessageType.Warning);
            }

            if (preview.StrandedConnections.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "These connections survive only as invalid ports, for you to re-wire: "
                    + string.Join(", ", preview.StrandedConnections), MessageType.Warning);
            }
        }

        private void DrawApply()
        {
            using (new EditorGUI.DisabledScope(selectedTarget == null || preview == null || !preview.CanApply))
            {
                if (GUILayout.Button($"Replace this node", GUILayout.Height(24.0f)))
                {
                    if (asset == null)
                    {
                        Debug.LogWarning("[BH3] no tree to retarget this node in.");
                    }
                    else if (MissingTypeRetarget.Apply(asset, placeholder, selectedTarget, out var failure))
                    {
                        Debug.Log($"[BH3] replaced a missing '{formerType}' node with {selectedTarget.Name}.", asset);
                        Close();
                        return;
                    }
                    else
                    {
                        Debug.LogError($"[BH3] could not replace the missing node: {failure}", asset);
                    }
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(formerType)))
                {
                    EditorGUILayout.BeginHorizontal();

                    if (GUILayout.Button("Apply to all in this tree") && asset != null)
                    {
                        int converted = MissingTypeRetarget.ApplyToTree(asset, formerType, selectedTarget);
                        Debug.Log($"[BH3] replaced {converted} '{formerType}' node(s) with {selectedTarget.Name}.", asset);
                        Close();
                        EditorGUILayout.EndHorizontal();
                        return;
                    }

                    if (GUILayout.Button("Apply across the project"))
                    {
                        var result = MissingTypeRetarget.ApplyToProject(formerType, selectedTarget);
                        Debug.Log($"[BH3] replaced '{formerType}' with {selectedTarget.Name}: {result}.");
                        Close();
                        EditorGUILayout.EndHorizontal();
                        return;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(formerType)))
            {
                if (GUILayout.Button("Copy [RenamedFrom] attribute to clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = $"[RenamedFrom(\"{formerType}\")]";

                    Debug.Log($"[BH3] copied [RenamedFrom(\"{formerType}\")] — put it on the class that "
                              + "replaced this type and every tree still naming the old one restores itself "
                              + "on load, including trees nobody has opened.");
                }
            }
        }

        /// <summary>
        /// The replacement picker. Grouped by the canvas's own create-menu paths so the choice reads the same
        /// way as placing a node by hand, with the likely answer promoted above the groups.
        /// </summary>
        private sealed class NodeTypeDropdown : AdvancedDropdown
        {
            private readonly List<Type> types;
            private readonly string formerShortName;
            private readonly Action<Type> onPicked;
            private readonly Dictionary<int, Type> byItemId = new Dictionary<int, Type>();

            public NodeTypeDropdown(
                AdvancedDropdownState state, List<Type> types, string formerShortName, Action<Type> onPicked)
                : base(state)
            {
                this.types = types;
                this.formerShortName = formerShortName;
                this.onPicked = onPicked;

                minimumSize = new Vector2(320.0f, 320.0f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Replacement type");
                int id = 0;

                var moved = types
                    .Where(type => string.Equals(type.Name, formerShortName, StringComparison.Ordinal))
                    .ToList();

                foreach (var type in moved)
                {
                    var item = new AdvancedDropdownItem($"{type.Name}  (same name, moved)") { id = ++id };
                    byItemId[item.id] = type;
                    root.AddChild(item);
                }

                if (moved.Count > 0) root.AddSeparator();

                var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);

                foreach (var type in types.Except(moved))
                {
                    string menu = NodeUtil.GetNodeGraphCreateMenu(type) ?? type.Name;

                    int slash = menu.LastIndexOf('/');
                    string groupName = slash < 0 ? "Other" : menu.Substring(0, slash);
                    string leafName = slash < 0 ? menu : menu.Substring(slash + 1);

                    if (!groups.TryGetValue(groupName, out var group))
                    {
                        group = new AdvancedDropdownItem(groupName);
                        groups[groupName] = group;
                        root.AddChild(group);
                    }

                    var item = new AdvancedDropdownItem(leafName) { id = ++id };
                    byItemId[item.id] = type;
                    group.AddChild(item);
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (byItemId.TryGetValue(item.id, out var type)) onPicked?.Invoke(type);
            }
        }
    }
}

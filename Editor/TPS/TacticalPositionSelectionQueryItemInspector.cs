using ArcaneOnyx.UnityTacticalPositionSelection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Draws a query-item slot as a dropdown over every query in the project's TPS databases, so picking
    /// one is choosing from what exists rather than dragging an object nobody can enumerate.
    ///
    /// <para>
    /// An <c>Inspector</c> rather than a <c>PropertyDrawer</c> for the reason
    /// <see cref="BTScriptGraphVariableInspector"/> documents: the drawer bridge hands a throwaway host
    /// object, while a registered <c>Inspector</c> receives the real metadata chain. The assignment runs
    /// from the dropdown's own popup window, outside the <c>BeginEdit/EndEdit</c> bracket, so the edited
    /// owner is captured during the draw and the undo is recorded through the same override
    /// <see cref="FunctionAssignment"/> uses — <c>RecordEditedObject</c> called bare from a popup callback
    /// records nothing and the pick evaporates on the next domain reload.
    /// </para>
    /// </summary>
    [Inspector(typeof(TacticalPositionSelectionQueryItem))]
    public class TacticalPositionSelectionQueryItemInspector : Inspector
    {
        public TacticalPositionSelectionQueryItemInspector(Metadata metadata) : base(metadata) { }

        private readonly AdvancedDropdownState dropdownState = new AdvancedDropdownState();

        /// <summary>The object Visual Scripting is editing, knowable only while drawing — see the precedent.</summary>
        private UnityEngine.Object editedOwner;

        protected override float GetHeight(float width, GUIContent label) => EditorGUIUtility.singleLineHeight;

        protected override void OnGUI(Rect position, GUIContent label)
        {
            var valueRect = BeginLabeledBlock(metadata, position, label);

            editedOwner = LudiqEditorUtility.editedObject.value;

            var item = metadata.value as TacticalPositionSelectionQueryItem;

            var content = new GUIContent(
                item != null ? item.Name : "None",
                item != null
                    ? AssetDatabase.GetAssetPath(item)
                    : "Queries from every TPS query database in the project.");

            if (EditorGUI.DropdownButton(valueRect, content, FocusType.Keyboard))
            {
                var dropdown = new QueryDropdown(dropdownState, Assign);
                dropdown.Show(valueRect);
            }

            EndBlock(metadata);
        }

        private void Assign(TacticalPositionSelectionQueryItem item)
        {
            if (editedOwner == null)
            {
                Debug.LogWarning(
                    "[BehaviorTree] The query picker could not identify the asset or scene object that owns "
                    + "this tree, so the selection was not applied rather than applied without an undo entry.");
                return;
            }

            using (LudiqEditorUtility.editedObject.Override(editedOwner))
            {
                UndoUtility.RecordEditedObject(item == null ? "Clear TPS Query" : "Assign TPS Query");
                metadata.value = item;
            }

            Authoring.NodeProblemCache.Invalidate();
            SetHeightDirty();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        // ------------------------------------------------------------------ the dropdown

        private sealed class QueryDropdownItem : AdvancedDropdownItem
        {
            public readonly TacticalPositionSelectionQueryItem Item;

            public QueryDropdownItem(string name, TacticalPositionSelectionQueryItem item) : base(name)
            {
                Item = item;
            }
        }

        /// <summary>
        /// Every query item in the project, grouped by the database that holds it — the database is how
        /// designers already organise presets, and two same-named items in different databases stay
        /// distinguishable under their own headings.
        /// </summary>
        private sealed class QueryDropdown : AdvancedDropdown
        {
            private readonly System.Action<TacticalPositionSelectionQueryItem> assign;

            public QueryDropdown(AdvancedDropdownState state, System.Action<TacticalPositionSelectionQueryItem> assign)
                : base(state)
            {
                this.assign = assign;
                minimumSize = new Vector2(280.0f, 280.0f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Query");

                root.AddChild(new QueryDropdownItem("None", null));
                root.AddSeparator();

                var any = false;

                foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(TacticalPositionSelectionQueryItemDabatabase)}"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var database = AssetDatabase.LoadAssetAtPath<TacticalPositionSelectionQueryItemDabatabase>(path);
                    if (database == null || database.Count == 0) continue;

                    var group = new AdvancedDropdownItem(database.name);
                    var added = false;

                    foreach (var item in database.Items)
                    {
                        if (item == null) continue;

                        group.AddChild(new QueryDropdownItem(item.Name, item));
                        added = true;
                    }

                    if (!added) continue;

                    root.AddChild(group);
                    any = true;
                }

                if (!any)
                {
                    root.AddChild(new AdvancedDropdownItem("No query items in this project yet") { enabled = false });
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is QueryDropdownItem selected) assign(selected.Item);
            }
        }
    }
}

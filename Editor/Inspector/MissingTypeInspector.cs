using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The inspector for a node whose type is gone: what it was, what it still holds, and the way out.
    ///
    /// <para>
    /// The reflected inspector would draw this node's three bookkeeping fields as editable text, which is
    /// both useless and dangerous — <c>formerValue</c> is the only remaining copy of the node's data. This
    /// replaces it with the thing an author actually needs first: evidence that nothing was lost.
    /// </para>
    ///
    /// <para>
    /// Choosing the replacement is <see cref="MissingTypeRetargetWindow"/> rather than more rows here. The
    /// canvas menu and the broken-tree finder need the same picker, and three copies of a preview is three
    /// chances for one of them to disagree with what Apply actually does.
    /// </para>
    /// </summary>
    [Inspector(typeof(MissingType))]
    public class MissingTypeInspector : Inspector
    {
        public MissingTypeInspector(Metadata metadata) : base(metadata) { }

        private static float Row => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        private bool showPreservedState = true;

        private MissingType Placeholder => metadata.value as MissingType;

        protected override float GetHeight(float width, GUIContent label)
        {
            var placeholder = Placeholder;
            if (placeholder == null) return Row;

            float height = LudiqGUIUtility.GetHelpBoxHeight(placeholder.Description, MessageType.Error, width);

            height += Spacing + Row;                        // former type
            height += Spacing + Row;                        // preserved-state foldout

            if (showPreservedState) height += PreservedStateHeight(placeholder);

            height += Spacing * 2.0f + Row;                 // replace button
            height += Spacing + Row;                        // copy [RenamedFrom]

            return height;
        }

        private static float PreservedStateHeight(MissingType placeholder)
        {
            int rows = placeholder.HasPreservedState
                ? PreservedMembers(placeholder).Count + (placeholder.formerObjects?.Count ?? 0)
                : 1;

            return Mathf.Max(1, rows) * (Row + Spacing);
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            var placeholder = Placeholder;
            if (placeholder == null) return;

            var row = position;

            row.height = LudiqGUIUtility.GetHelpBoxHeight(placeholder.Description, MessageType.Error, position.width);
            EditorGUI.HelpBox(row, placeholder.Description, MessageType.Error);
            row.y += row.height + Spacing;

            row.height = Row;

            DrawLabelledValue(row, "Former type",
                string.IsNullOrEmpty(placeholder.formerType) ? "(unknown)" : placeholder.formerType);
            row.y += row.height + Spacing;

            showPreservedState = EditorGUI.Foldout(row, showPreservedState, PreservedStateTitle(placeholder), true);
            row.y += row.height + Spacing;

            if (showPreservedState) DrawPreservedState(ref row, placeholder);

            row.y += Spacing;

            if (GUI.Button(row, "Replace missing type…"))
            {
                // Deferred: opening a window from inside an inspector's OnGUI throws on the layout event.
                var captured = placeholder;
                EditorApplication.delayCall += () => MissingTypeRetargetWindow.Open(captured);
            }

            row.y += row.height + Spacing;

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(placeholder.formerType)))
            {
                if (GUI.Button(row, "Copy [RenamedFrom] attribute to clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = $"[RenamedFrom(\"{placeholder.formerType}\")]";

                    Debug.Log($"[BH3] copied [RenamedFrom(\"{placeholder.formerType}\")] — put it on the "
                              + "class that replaced this type and every tree still naming the old one "
                              + "restores itself on load, including trees nobody has opened.");
                }
            }
        }

        private static string PreservedStateTitle(MissingType placeholder)
        {
            if (!placeholder.HasPreservedState) return "Preserved state — nothing was kept for this node";

            int members = PreservedMembers(placeholder).Count;
            int objects = placeholder.formerObjects?.Count ?? 0;

            return $"Preserved state — {members} value(s), {objects} object reference(s)";
        }

        private static void DrawPreservedState(ref Rect row, MissingType placeholder)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                if (!placeholder.HasPreservedState)
                {
                    EditorGUI.LabelField(Indent(row),
                        "Written before placeholders kept anything; only position and wiring survive.");
                    row.y += row.height + Spacing;
                    return;
                }

                foreach (var member in PreservedMembers(placeholder))
                {
                    EditorGUI.LabelField(Indent(row), member.Key, member.Value);
                    row.y += row.height + Spacing;
                }

                if (placeholder.formerObjects == null) return;

                foreach (var reference in placeholder.formerObjects)
                {
                    EditorGUI.ObjectField(Indent(row), " ", reference, typeof(UnityEngine.Object), false);
                    row.y += row.height + Spacing;
                }
            }
        }

        private static Rect Indent(Rect row)
        {
            row.xMin += 12.0f;
            return row;
        }

        /// <summary>
        /// The node's own serialized members, read straight out of what was preserved. Shown because the
        /// first question anyone asks a placeholder is whether their work is still in there.
        /// </summary>
        private static List<KeyValuePair<string, string>> PreservedMembers(MissingType placeholder)
        {
            var members = new List<KeyValuePair<string, string>>();

            if (!placeholder.HasPreservedState) return members;
            if (!fsJsonParser.Parse(placeholder.formerValue, out var data).Succeeded || !data.IsDictionary) return members;

            foreach (var pair in data.AsDictionary)
            {
                // The same answer the retarget preview uses, from the same place. The two are read side by
                // side by someone deciding whether to commit, so they must not be able to disagree.
                if (MissingTypeSerialization.IsBaseNodeMember(pair.Key)) continue;

                members.Add(new KeyValuePair<string, string>(pair.Key, Describe(pair.Value)));
            }

            return members;
        }

        private static string Describe(fsData value)
        {
            if (value == null || value.IsNull) return "null";
            if (value.IsString) return value.AsString;
            if (value.IsBool) return value.AsBool.ToString();
            if (value.IsInt64) return value.AsInt64.ToString();
            if (value.IsDouble) return value.AsDouble.ToString("0.###");
            if (value.IsList) return $"[{value.AsList.Count} item(s)]";

            return value.IsDictionary ? $"{{{value.AsDictionary.Count} field(s)}}" : value.ToString();
        }

        private static void DrawLabelledValue(Rect row, string label, string value)
        {
            var labelRect = new Rect(row.x, row.y, EditorGUIUtility.labelWidth, row.height);
            var valueRect = new Rect(labelRect.xMax, row.y, row.width - labelRect.width, row.height);

            EditorGUI.LabelField(labelRect, label);
            EditorGUI.SelectableLabel(valueRect, value, EditorStyles.textField);
        }
    }
}

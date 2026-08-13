using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Draws a <see cref="GuardTrigger"/> as the kind it actually is.
    ///
    /// <para>
    /// Without a registered inspector the trigger list renders as <i>"No Inspector for Guard Trigger"</i>.
    /// Visual Scripting resolves an inspector <b>per type</b> and has no fallback for an arbitrary class:
    /// <c>[Inspectable]</c> on the members only says which members are eligible once something is drawing the
    /// type, and <c>[Serialize]</c> only concerns the serializer. Neither registers a drawer. Unity's own
    /// inspector generation does not reach it either — a trigger is stored inside a
    /// <see cref="ReactiveGuard"/>, serialized by Visual Scripting rather than by the
    /// <c>[SerializeField]</c> path, so there is no <c>SerializedProperty</c> for a Unity
    /// <c>PropertyDrawer</c> to bind to.
    /// </para>
    ///
    /// <para>
    /// A plain <see cref="ReflectedInspector"/> works but draws all six members at once, so a trigger set to
    /// <see cref="GuardTriggerKind.OnKeyChanged"/> still shows Seconds and Deviation — fields
    /// that do nothing, crowding the two that matter inside a list row that is already narrow. A trigger is
    /// exactly one kind, so only that kind's fields are drawn. That is also the honest reading of the union:
    /// the other members are not "empty", they are not part of this trigger at all.
    /// </para>
    /// </summary>
    [Inspector(typeof(GuardTrigger))]
    public class GuardTriggerInspector : Inspector
    {
        public GuardTriggerInspector(Metadata metadata) : base(metadata) { }

        /// <summary>
        /// Labels are held to a share of the row rather than Unity's default, which is sized for a full
        /// inspector window and leaves almost nothing for the value inside a nested list element.
        /// </summary>
        private const float LabelFraction = 0.42f;

        private const float MinLabelWidth = 64.0f;
        private const float MaxLabelWidth = 130.0f;

        private Metadata Kind => metadata[nameof(GuardTrigger.Kind)];

        /// <summary>The members belonging to the kind currently selected, in the order they should read.</summary>
        private IEnumerable<Metadata> FieldsForKind()
        {
            var kind = Kind.value is GuardTriggerKind selected ? selected : GuardTriggerKind.OnKeyChanged;

            switch (kind)
            {
                case GuardTriggerKind.OnKeyChanged:
                    yield return metadata[nameof(GuardTrigger.Keys)];
                    break;

                case GuardTriggerKind.EveryInterval:
                    yield return metadata[nameof(GuardTrigger.Seconds)];
                    yield return metadata[nameof(GuardTrigger.Deviation)];
                    break;

                // EveryFrame carries no configuration -- the kind is the whole statement.
            }
        }

        protected override float GetHeight(float width, GUIContent label)
        {
            float height = LudiqGUI.GetInspectorHeight(this, Kind, width) + 10;

            foreach (var field in FieldsForKind())
            {
                height += EditorGUIUtility.standardVerticalSpacing;
                height += LudiqGUI.GetInspectorHeight(this, field, width);
            }

            return height;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            BeginLabeledBlock(metadata, position, label);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth =
                Mathf.Clamp(position.width * LabelFraction, MinLabelWidth, MaxLabelWidth);

            try
            {
                LudiqGUI.Inspector(Kind, position.VerticalSection(ref y, LudiqGUI.GetInspectorHeight(this, Kind, position.width)));

                foreach (var field in FieldsForKind())
                {
                    y += EditorGUIUtility.standardVerticalSpacing;

                    LudiqGUI.Inspector(
                        field,
                        position.VerticalSection(ref y, LudiqGUI.GetInspectorHeight(this, field, position.width)));
                }
            }
            finally
            {
                // Restored even if a nested inspector throws: label width is global editor state, and leaking
                // it would silently mis-lay-out every inspector drawn after this one.
                EditorGUIUtility.labelWidth = previousLabelWidth;
            }

            EndBlock(metadata);
        }
    }
}

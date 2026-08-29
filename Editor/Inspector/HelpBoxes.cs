using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Measures a help box the way <c>EditorGUI.HelpBox</c> will draw it, for rect-based inspectors whose
    /// <c>GetHeight</c> and <c>OnGUI</c> must agree.
    /// </summary>
    internal static class HelpBoxes
    {
        /// <summary>
        /// How tall a help box is for this text at this width. Measured with the icon in the content,
        /// because <c>EditorGUI.HelpBox</c> draws one and it takes a column off the text, so a text-only
        /// measurement wraps one line too few. The floor is the icon's own height, or a one-line message
        /// draws a box too short for its own picture.
        /// </summary>
        public static float HeightFor(string text, float width, MessageType type)
        {
            var icon = type == MessageType.Error
                ? EditorGUIUtility.IconContent("console.erroricon").image
                : EditorGUIUtility.IconContent("console.warnicon").image;

            var measured = EditorStyles.helpBox.CalcHeight(new GUIContent(text, icon), width);

            return Mathf.Max(measured, MinHeight);
        }

        private const float MinHeight = 38.0f;
    }
}

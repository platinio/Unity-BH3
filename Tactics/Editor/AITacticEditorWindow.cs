using System.Collections.Generic;
using ArcaneOnyx.ScriptableObjectDatabase;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.AIDesigner
{
    public class AITacticEditorWindow : DatabaseEditorWindow<AITacticDatabase, AITactic>
    {
        [MenuItem("Window/AI Designer/AI Tactic Editor")]
        public static void OpenEditor()
        {
            AITacticEditorWindow wnd = GetWindow<AITacticEditorWindow>();
            wnd.titleContent = new GUIContent(wnd.GetWindowTitle());
        }

        public override string GetWindowTitle() => "AITactic Editor";

        protected override IReadOnlyList<AITactic> FilterEntries(IReadOnlyList<AITactic> entries) => entries;
    }
}
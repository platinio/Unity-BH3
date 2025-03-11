using ArcaneOnyx.ScriptableObjectDatabase;
using UnityEditor;

namespace ArcaneOnyx.AIDesigner
{
    [CustomEditor(typeof(AITactic))]
    public class AITactictEditor : ScriptableItemUIToolkitEditor<AITacticEditorWindow, AITacticDatabase, AITactic> { }
}
using ArcaneOnyx.ScriptableObjectDatabase;
using UnityEditor;

namespace ArcaneOnyx.AIDesigner
{
    [CustomEditor(typeof(AITacticDatabase))]
    public class AITacticDatabaseEditor : ScriptableDatabaseEditor<AITacticEditorWindow, AITacticDatabase, AITactic> { }
}
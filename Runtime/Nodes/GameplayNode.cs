using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Base action node for behavior trees
    /// </summary>
    public class GameplayNode : BehaviorTreeNode
    {
        protected void SaveVariable(string key, VariableKind variableKind, object value)
        {
            // Recorded before the write, because afterwards the previous value is gone and "what did it change
            // from" is half of what makes a write worth recording. The call and everything inside its
            // arguments — the read included — are removed by the compiler outside the editor and dev builds,
            // so the extra lookup does not exist in a shipped build.
            Debugging.BehaviorTreeRecorder.VariableWrite(this, key, ReadVariable(key, variableKind), value);

            switch (variableKind)
            {
                // Writes land in this node's own scope and go no further. A branch cannot reach its caller's
                // variables, so scratch state cannot leak sideways into a sibling which is what makes the
                // same branch safe to reuse across unrelated agents. State that genuinely belongs to the whole
                // agent has a home already: VariableKind.Object, on the agent's Variables component.
                case VariableKind.Graph:
                    VariableScope?.Set(key, value);
                    break;
                case VariableKind.Object:
                    BehaviorTreeMachine.Variables.declarations.Set(key, value);
                    break;
                case VariableKind.Scene:
                    SceneVariables.Instance(SceneManager.GetActiveScene()).variables.declarations.Set(key, value);
                    break;
                case VariableKind.Application:
                    ApplicationVariables.current.Set(key, value);
                    break;
                case VariableKind.Saved:
                    SavedVariables.current.Set(key, value);
                    break;
                case VariableKind.Flow:
                    Debug.LogError($"BehaviorTree doesnt support Flow VariableKind Node={NodeName} Key={key}");
                    break;
            }
        }

        /// <summary>
        /// The value a write is about to replace, or null when there isn't one yet.
        /// <para>
        /// Never throws. Every store here throws on an undefined name, and a debugging read has no business
        /// turning a first write into an exception — so an undeclared variable reads as null, which is what a
        /// recording should say about a value that did not exist.
        /// </para>
        /// </summary>
        private object ReadVariable(string key, VariableKind variableKind)
        {
            if (string.IsNullOrEmpty(key)) return null;

            switch (variableKind)
            {
                case VariableKind.Graph:
                    return VariableScope != null && VariableScope.TryGet(key, out var scoped) ? scoped : null;
                case VariableKind.Object:
                    return Read(BehaviorTreeMachine != null ? BehaviorTreeMachine.Variables?.declarations : null, key);
                case VariableKind.Scene:
                    return Read(SceneVariables.Instance(SceneManager.GetActiveScene())?.variables?.declarations, key);
                case VariableKind.Application:
                    return Read(ApplicationVariables.current, key);
                case VariableKind.Saved:
                    return Read(SavedVariables.current, key);
                default:
                    return null;
            }
        }

        private static object Read(VariableDeclarations declarations, string key)
        {
            return declarations != null && declarations.IsDefined(key) ? declarations.Get(key) : null;
        }
    }
}

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
            switch (variableKind)
            {
                // Writes land in this node's own scope and go no further. A branch cannot reach its caller's
                // variables, so scratch state cannot leak sideways into a sibling — which is what makes the
                // same branch safe to reuse across unrelated agents. State that genuinely belongs to the whole
                // agent has a home already: VariableKind.Object, on the agent's Variables component.
                //
                // This previously wrote to BehaviorTreeMachine.GraphAsset — the original macro rather than the
                // running instance, so a different dictionary from the one GetVariable reads, and an asset
                // that would be mutated on disk in the editor.
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
    }
}
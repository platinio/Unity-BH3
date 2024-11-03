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
                case VariableKind.Graph:
                    BehaviorTreeMachine.GraphAsset.declarations.Set(key, value);
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
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Platinio.BehaviourTree
{
    public class VariablesUtil
    {
        public static List<string> GetVariablesName(VariableKind kind)
        {
            if (kind == VariableKind.Scene)
            {
                return GetSceneVariableNames();
            }

            return null;
        }

        private static List<string> GetSceneVariableNames()
        {
            List<string> result = new List<string>();
            var sceneVariables = SceneVariables.Instance(SceneManager.GetActiveScene());
            foreach (var variable in sceneVariables.variables.declarations)
            {
                result.Add(variable.name);
            }

            return result;
        }
    }
}
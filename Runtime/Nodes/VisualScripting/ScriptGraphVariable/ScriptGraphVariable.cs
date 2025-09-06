using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx
{
    [System.Serializable]
    public class ScriptGraphVariable
    {
        [SerializeField] protected ScriptGraphAsset scriptGraphAsset;

        public ScriptGraphAsset ScriptGraphAsset => scriptGraphAsset;
        
        public T GetValue<T>(Variables input = null)
        {
            return scriptGraphAsset.GetScriptGraphOutput<T>(input);
        }
        
        public T GetValue<T>(GameObject gameObject, Variables input = null)
        {
            return scriptGraphAsset.GetScriptGraphOutput<T>(input, gameObject);
        }
        
        public T GetValue<T>(GameObject gameObject, VariableDeclarations variableDeclarations)
        {
            Dictionary<string, object> dynamicParameters = new();

            foreach (var variableDeclaration in variableDeclarations)
            {
                dynamicParameters[variableDeclaration.name] = variableDeclaration.value;
            }

            return scriptGraphAsset.GetScriptGraphOutput<T>(dynamicParameters, gameObject);
        }

        public void Run(Variables input = null)
        {
            scriptGraphAsset.Run(input);
        }
        
        public void Run(GameObject gameObject, Variables input = null)
        {
            scriptGraphAsset.Run(input, gameObject);
        }
        
        public void Run(GameObject gameObject, VariableDeclarations input = null)
        {
            scriptGraphAsset.Run(input, gameObject);
        }

    }
}


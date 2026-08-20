using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public class BaseVisualScriptingNode : GameplayNode
    {
        [Serialize] private List<BTScriptGraphVariable> scriptGraphVariables = new List<BTScriptGraphVariable>();
        [Serialize] [Inspectable] protected string comment = "";
        
        //nodes contaning script graph asset cant be copied, duplicated, cut otherwise they will share the exact script graph asset
        //which will be deleted if any of the copies gets deleted
        public override bool CanCopy => false;
        public override bool CanCut => false;
        public override bool CanDuplicate => false;

        public override IEnumerable<ScriptGraphAsset> scriptGraphAssets 
        {
            get
            {
                List<ScriptGraphAsset> scriptGraphAssets = new List<ScriptGraphAsset>();

                foreach (var scriptGraphVariable in scriptGraphVariables)
                {
                    if (scriptGraphVariable.ScriptGraphAsset == null) continue;
                    scriptGraphAssets.Add(scriptGraphVariable.ScriptGraphAsset);
                }

                return scriptGraphAssets;
            }
        }

        protected BTScriptGraphVariable CreateRunnableScriptGraphVariable()
        {
            var scriptGraphVariable = new BTScriptGraphVariable();
            scriptGraphVariables.Add(scriptGraphVariable);
            return scriptGraphVariable;
        }
        
        protected BTScriptGraphVariable CreateGraphWithOutput(Type returnType)
        {
            var scriptGraphVariable = new BTScriptGraphVariable(returnType);
            scriptGraphVariables.Add(scriptGraphVariable);
            return scriptGraphVariable;
        }
    }
}
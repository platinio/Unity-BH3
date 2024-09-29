using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [CreateAssetMenu(menuName = "Visual Scripting/Behavior Tree", fileName = "New Behavior Tree Graph", order = 81)]
    public class BehaviorTreeGraphAsset : BaseGraphAsset<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        [Serialize, Inspectable]
        public VariableDeclarations declarations { get; internal set; } = new() { Kind = VariableKind.Graph };
        
        //[ContextMenu("Show Data...")]
        protected override void ShowData()
        {
            base.ShowData();
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return BehaviorTreeGraph.CreateEmpty();
        }
    
        protected override void OnBeforeDeserialize()
        {
            string newJson = _data.json;
            bool updateData = false;
            
            foreach (var startIndex in _data.json.AllIndexesOf("\"$type\":\""))
            {
                int index = startIndex + 9;
                int endIndex = 0;

                for (int i = 0; i < _data.json.Length - index; i++)
                {
                    var c = _data.json[index + i];
                    if (c == '"')
                    {
                        endIndex = index + i - 1;
                        break;
                    }
                }

                string typeStr = _data.json.Substring(startIndex + 9, endIndex - startIndex - 8);
                if (typeStr.Contains("[")) continue;

                if (!RuntimeCodebase.TryDeserializeType(typeStr, out var type))
                {
                    updateData = true;
                    Debug.Log($"type {typeStr} is missing updating it");
                    typeStr = $"\"$type\":\"{typeStr}\"";
                    string replaceStr = $"\"$type\":\"ArcaneOnyx.BehaviorTree.MissingType\"";
                    newJson = newJson.Replace(typeStr, replaceStr);
                }
            }

            if (updateData)
            {
                SerializationData newData = new SerializationData(newJson, _data.objectReferences);
                _data = newData;
            }
            
            base.OnBeforeDeserialize();
        }
    }
}
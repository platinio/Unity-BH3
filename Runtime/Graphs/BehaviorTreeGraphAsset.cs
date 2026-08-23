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

        /// <summary>
        /// What an agent running this tree must declare on its Variables component. Reading a variable that
        /// nothing supplies throws, so a branch reused on an agent that lacks one fails at the tick rather
        /// than at author time — this is the list that makes that checkable before play.
        /// <para>
        /// Nothing is read from here at runtime. It is a contract, and the values only carry the type so the
        /// Blackboard panel can edit it with the same inspector as any other declaration.
        /// </para>
        /// </summary>
        [Serialize, Inspectable]
        public VariableDeclarations requiredDeclarations { get; internal set; } = new() { Kind = VariableKind.Graph };

        /// <summary>
        /// Values this tree supplies for itself when the agent does not. Applied to the root tree's
        /// declarations by <c>BehaviorTreeMachine.OverrideGraphVariables</c>, and always losing to
        /// the agent, so a branch can ship a sensible default and only genuinely agent-specific values need
        /// to appear in <see cref="requiredDeclarations"/>.
        /// </summary>
        [Serialize, Inspectable]
        public VariableDeclarations optionalDeclarations { get; internal set; } = new() { Kind = VariableKind.Graph };

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
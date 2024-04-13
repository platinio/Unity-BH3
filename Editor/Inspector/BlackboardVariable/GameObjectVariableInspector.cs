using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Platinio.BehaviorTree
{
    [Inspector(typeof(GameObjectBlackboardVariable))]
    public class GameObjectBlackboardVariableInspector : BlackboardVariableInspector<GameObject>
    {
        public GameObjectBlackboardVariableInspector(Metadata metadata) : base(metadata)
        {
           
        }

        protected override GameObject ValueField(Rect position, string name, GameObject value)
        {
            return (GameObject) EditorGUI.ObjectField(position, GUIContent.none, value, typeof(GameObject));
        }
    }
}
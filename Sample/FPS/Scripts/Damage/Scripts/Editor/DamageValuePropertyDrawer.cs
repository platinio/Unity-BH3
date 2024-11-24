using UnityEditor;
using UnityEngine;

namespace RPGDamage
{
    //TODO: FIX THIS, we are using EditorguiLayout in a rect base drawer
    //[CustomPropertyDrawer(typeof(DamageValue))]
    public class DamageValuePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position,label,property);
            
            EditorGUILayout.LabelField(label);
            
            var useConstValue = property.FindPropertyRelative("UseConstValue");
            var constValue = property.FindPropertyRelative("ConstValue");
            var minValue = property.FindPropertyRelative("MinValue");
            var maxValue = property.FindPropertyRelative("MaxValue");
            var damageType = property.FindPropertyRelative("DamageType");

            EditorGUILayout.PropertyField(useConstValue);
            if (useConstValue.boolValue)
            {
                EditorGUILayout.PropertyField(constValue);
            }
            else
            {
                EditorGUILayout.PropertyField(minValue);
                EditorGUILayout.PropertyField(maxValue);
            }

            EditorGUILayout.PropertyField(damageType);
            
            EditorGUI.EndProperty();
        }
    }
}


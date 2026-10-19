using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Writing to a component inside a prefab asset.
    ///
    /// <para>
    /// <c>set_component_properties</c> works on a component in a loaded scene, so authoring a prefab through
    /// it means instantiating the prefab, editing the instance and applying the overrides back: three steps
    /// that put a temporary object in somebody's scene to change an asset. Agents route around that with
    /// hand-written <c>eval</c> scripts, which is how a one-off loop ends up editing more than it was asked
    /// to.
    /// </para>
    /// </summary>
    public static class PrefabAuthoringCommands
    {
        [CliCommand("prefab_set_properties",
            "Set serialized properties on a component inside a prefab asset: the prefab is opened, written " +
            "and saved, with its nested prefabs intact and without putting anything in a scene. Takes the " +
            "same property names set_component_properties takes — the ones the inspector shows, which for a " +
            "Unity component are the m_ names. An object reference is given as the asset path of what it " +
            "should point at, or null to clear it; a list is given whole, as an array.")]
        public static object SetPrefabPropertiesCommand(
            [CliArg("prefab", "Asset path of the prefab, e.g. Assets/Game/Prefabs/Soldier.prefab.", Required = true)] string prefab,
            [CliArg("type", "Component type name on the target object, e.g. Health or NavMeshAgent.", Required = true)] string type,
            [CliArg("properties", "Serialized property name to value, e.g. {\"maxHealth\": 120}.", Required = true)] JObject properties,
            [CliArg("child", "Path of a child under the prefab root, e.g. Body/Head. Omit for the root itself.")] string child = null)
        {
            if (properties == null || properties.Count == 0)
                throw new ArgumentException("'properties' must name at least one property to set.");

            var path = ResolvePrefabPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var target = ObjectUnder(root, child);
                var component = ComponentOn(target, type);
                var written = Write(component, properties);

                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Unity refused to save '{path}'.");

                return new { prefab = path, child = target.name, type = component.GetType().Name, written };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ResolvePrefabPath(string prefab)
        {
            var path = prefab.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? prefab : prefab + ".prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                throw new ArgumentException($"No prefab at '{path}'.");

            return path;
        }

        private static GameObject ObjectUnder(GameObject root, string child)
        {
            if (string.IsNullOrEmpty(child)) return root;

            var found = root.transform.Find(child);
            if (found == null)
                throw new ArgumentException(
                    $"No child '{child}' under '{root.name}'. It has: {string.Join(", ", ChildPathsOf(root.transform, string.Empty))}.");

            return found.gameObject;
        }

        private static IEnumerable<string> ChildPathsOf(Transform parent, string prefix)
        {
            foreach (Transform child in parent)
            {
                var path = prefix + child.name;
                yield return path;

                foreach (var deeper in ChildPathsOf(child, path + "/")) yield return deeper;
            }
        }

        private static Component ComponentOn(GameObject target, string type)
        {
            var component = target.GetComponents<Component>()
                .FirstOrDefault(each => each != null && (each.GetType().Name == type || each.GetType().FullName == type));

            if (component == null)
                throw new ArgumentException(
                    $"'{target.name}' has no {type}. It has: " +
                    $"{string.Join(", ", target.GetComponents<Component>().Where(each => each != null).Select(each => each.GetType().Name))}.");

            return component;
        }

        private static string[] Write(Component component, JObject properties)
        {
            var serialized = new SerializedObject(component);

            foreach (var pair in properties)
            {
                var property = serialized.FindProperty(pair.Key);
                if (property == null)
                    throw new ArgumentException($"{component.GetType().Name} has no serialized property '{pair.Key}'.");

                Write(property, pair.Value);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);

            return properties.Properties().Select(property => property.Name).ToArray();
        }

        private static void Write(SerializedProperty property, JToken value)
        {
            if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
            {
                WriteEvery(property, value as JArray);
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: property.boolValue = value.Value<bool>(); return;
                case SerializedPropertyType.Integer: property.intValue = value.Value<int>(); return;
                case SerializedPropertyType.Float: property.floatValue = value.Value<float>(); return;
                case SerializedPropertyType.String: property.stringValue = value.Value<string>(); return;
                case SerializedPropertyType.Enum: property.enumValueIndex = EnumIndexOf(property, value); return;
                case SerializedPropertyType.Vector2: property.vector2Value = (Vector2)Numbers(value, 2); return;
                case SerializedPropertyType.Vector3: property.vector3Value = Numbers(value, 3); return;
                case SerializedPropertyType.Vector4: property.vector4Value = Numbers(value, 4); return;
                case SerializedPropertyType.Color: property.colorValue = ColorFrom(value); return;
                case SerializedPropertyType.ObjectReference: property.objectReferenceValue = AssetAt(value); return;

                default:
                    throw new ArgumentException(
                        $"'{property.propertyPath}' is a {property.propertyType}, which this command does not know how to write.");
            }
        }

        /// <summary>A list is written whole, so what it holds afterwards is what was asked for.</summary>
        private static void WriteEvery(SerializedProperty property, JArray values)
        {
            if (values == null)
                throw new ArgumentException($"'{property.propertyPath}' is a list, so it takes a list of values.");

            property.arraySize = values.Count;

            for (var index = 0; index < values.Count; index++) Write(property.GetArrayElementAtIndex(index), values[index]);
        }

        private static int EnumIndexOf(SerializedProperty property, JToken value)
        {
            if (value.Type == JTokenType.Integer) return value.Value<int>();

            var name = value.Value<string>();
            var index = Array.FindIndex(property.enumNames, each => each.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
                throw new ArgumentException($"'{name}' is not one of {property.propertyPath}'s values: {string.Join(", ", property.enumNames)}.");

            return index;
        }

        private static Vector4 Numbers(JToken value, int expected)
        {
            if (!(value is JArray numbers) || numbers.Count != expected)
                throw new ArgumentException($"Expected {expected} numbers, as [x, y, z], and got '{value}'.");

            var read = new float[4];
            for (var axis = 0; axis < expected; axis++) read[axis] = numbers[axis].Value<float>();

            return new Vector4(read[0], read[1], read[2], read[3]);
        }

        private static Color ColorFrom(JToken value)
        {
            var channels = (JArray)value;
            var alpha = channels.Count > 3 ? channels[3].Value<float>() : 1.0f;

            return new Color(channels[0].Value<float>(), channels[1].Value<float>(), channels[2].Value<float>(), alpha);
        }

        private static UnityEngine.Object AssetAt(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return null;

            var path = value.Value<string>();
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            if (asset == null) throw new ArgumentException($"No asset at '{path}'.");

            return asset;
        }
    }
}

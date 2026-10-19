using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Authoring the variables an agent starts with.
    ///
    /// <para>
    /// Visual Scripting keeps them in one serialized blob, which the pipeline's own
    /// <c>set_component_properties</c> reports as <c>unsupported:Generic</c> and refuses to touch. Without
    /// this the only way to give a prefab a starting variable from outside the inspector is a hand-written
    /// script, or a component that writes it at <c>Awake</c> — which is state the prefab should have been
    /// carrying in the first place.
    /// </para>
    /// </summary>
    public static class AgentVariableCommands
    {
        [CliCommand("bt_set_agent_variable",
            "Give a prefab's agent a variable to start with, the way the Variables component's inspector " +
            "does: the prefab is opened, the declaration written and the prefab saved. A value is a number, " +
            "a bool, a string, [x, y, z] for a Vector3, or the asset path of an object. Use it for what a " +
            "tree reads about the agent itself — how far it likes to stand, which post it guards — instead " +
            "of a component that stamps the value on at Awake.")]
        public static object SetAgentVariableCommand(
            [CliArg("prefab", "Asset path of the prefab, e.g. Assets/Game/Prefabs/Archer.prefab.", Required = true)] string prefab,
            [CliArg("key", "Variable name, exactly as the tree reads it.", Required = true)] string key,
            [CliArg("value", "The value to start with.", Required = true)] JToken value,
            [CliArg("child", "Path of a child under the prefab root. Omit for the root, which is where an agent's variables live.")] string child = null)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("'key' cannot be blank.");

            var path = prefab.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? prefab : prefab + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) throw new ArgumentException($"No prefab at '{path}'.");

            var root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                var agent = string.IsNullOrEmpty(child) ? root : ChildUnder(root, child);
                var variables = agent.GetComponent<Variables>();

                if (variables == null)
                    throw new ArgumentException($"'{agent.name}' has no Variables component, so it holds no agent variables.");

                var written = ValueFrom(value);
                variables.declarations.Set(key, written);
                EditorUtility.SetDirty(variables);

                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Unity refused to save '{path}'.");

                return new { prefab = path, agent = agent.name, key, value = written?.ToString() ?? "null" };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject ChildUnder(GameObject root, string child)
        {
            var found = root.transform.Find(child);
            if (found == null) throw new ArgumentException($"No child '{child}' under '{root.name}'.");

            return found.gameObject;
        }

        private static object ValueFrom(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Boolean: return value.Value<bool>();
                case JTokenType.Integer: return value.Value<int>();
                case JTokenType.Float: return value.Value<float>();
                case JTokenType.Null: return null;
                case JTokenType.Array: return Vector3From((JArray)value);

                case JTokenType.String:
                    var text = value.Value<string>();
                    var asset = text.StartsWith("Assets/", StringComparison.Ordinal)
                        ? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(text)
                        : null;
                    return asset != null ? asset : (object)text;

                default:
                    throw new ArgumentException($"A {value.Type} is not a value an agent variable can hold.");
            }
        }

        private static Vector3 Vector3From(JArray numbers)
        {
            if (numbers.Count != 3) throw new ArgumentException($"Expected [x, y, z] and got '{numbers}'.");

            return new Vector3(numbers[0].Value<float>(), numbers[1].Value<float>(), numbers[2].Value<float>());
        }
    }
}

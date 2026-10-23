using System;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Pointing a prefab's machine at the tree it runs.
    ///
    /// <para>
    /// The nest a machine keeps its macro in is serialized the way Visual Scripting serializes everything —
    /// one JSON blob and a list of object references — so no general property command can reach it. Without
    /// this, a prefab gets its tree from the inspector or from a component that assigns one at
    /// <c>Awake</c>, and the second is how a prefab ends up not knowing what it runs.
    /// </para>
    /// </summary>
    public static class MachineTreeCommands
    {
        [CliCommand("bt_set_machine_tree",
            "Point a prefab's BehaviorTreeMachine at the tree it should run, the way the inspector's Macro " +
            "field does, and save the prefab. The machine keeps its macro in a serialized blob that " +
            "set_component_properties reports as unsupported, so this is the only way to author it from a " +
            "command.")]
        public static object SetMachineTreeCommand(
            [CliArg("prefab", "Asset path of the prefab, e.g. Assets/Game/Prefabs/Soldier.prefab.", Required = true)] string prefab,
            [CliArg("tree", "Asset path of the behavior tree the machine should run.", Required = true)] string tree,
            [CliArg("child", "Path of a child under the prefab root. Omit for the root, which is where a machine usually sits.")] string child = null)
        {
            var prefabPath = prefab.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? prefab : prefab + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) throw new ArgumentException($"No prefab at '{prefabPath}'.");

            var treePath = tree.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ? tree : tree + ".asset";
            var macro = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(treePath);
            if (macro == null) throw new ArgumentException($"No behavior tree at '{treePath}'.");

            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var agent = string.IsNullOrEmpty(child) ? root : ChildUnder(root, child);
                var machine = agent.GetComponent<BehaviorTreeMachine>();

                if (machine == null) throw new ArgumentException($"'{agent.name}' has no BehaviorTreeMachine.");

                machine.nest.macro = macro;
                EditorUtility.SetDirty(machine);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var saved);
                if (!saved) throw new InvalidOperationException($"Unity refused to save '{prefabPath}'.");

                return new { prefab = prefabPath, agent = agent.name, tree = macro.name };
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
    }
}

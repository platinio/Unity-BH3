using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ArcaneOnyx.BehaviorTree.Debugging;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Checks generated trees the way they will actually be loaded, in the same run that generated them.
    /// <para>
    /// The point is the reload. A value written by <c>SetDefaultValue</c> onto a port that declares no
    /// default is still sitting in memory for the rest of the generating run, so a dump taken there reports
    /// it as present and is blind to the one bug it exists to catch — the value is gone once the asset comes
    /// back from disk. <see cref="Reload"/> forces that round trip, which is what lets generate-and-verify be
    /// one Unity launch instead of two.
    /// </para>
    /// </summary>
    public static class BehaviorTreeVerification
    {
        /// <summary>
        /// Port keys that read as unset but never call <c>GetValue()</c> — they resolve through
        /// <c>GetComponent&lt;T&gt;(ValueInput)</c> and fall back to the machine's own GameObject, so an
        /// unset one is correct rather than a defect.
        /// </summary>
        private static readonly HashSet<string> PortsSafeToLeaveUnset = new() { "Animator", "Target" };

        /// <summary>
        /// Re-imports and re-loads a tree so it is the deserialized object, not the one still in memory.
        /// </summary>
        public static BehaviorTreeGraphAsset Reload(string assetPath)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var onDisk = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(assetPath);
            if (onDisk == null) return null;

            // LoadAssetAtPath hands back the instance already in memory, which still carries anything set on
            // it this run. Instantiate forces the serialize/deserialize round trip — and it is the same call
            // BehaviorTreeMachine.Awake makes on the macro, so what survives here is what reaches gameplay.
            return Object.Instantiate(onDisk);
        }

        /// <summary>
        /// Reloads each tree and reports everything the dump can see wrong with it. Returns one line per
        /// finding, empty when all of them are clean.
        /// </summary>
        public static List<string> Verify(params string[] assetPaths)
        {
            var findings = new List<string>();

            foreach (var path in assetPaths)
            {
                var asset = Reload(path);

                if (asset == null)
                {
                    findings.Add($"{path}: no behavior tree at this path.");
                    continue;
                }

                string json = BehaviorTreeDump.ToJson(asset);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                foreach (var port in UnsetPortsThatMatter(json))
                {
                    findings.Add($"{name}: port '{port}' is unset and will throw when read.");
                }

                findings.AddRange(ContractDrift(asset, name));

                findings.AddRange(Occurrences(json, "\"error\": \"([^\"]+)\"", name, "node reported"));
                findings.AddRange(Occurrences(json, "\"note\": \"(nothing reaches or reads this node)\"", name, "orphan"));
                findings.AddRange(Occurrences(json, "\"subTree\": \"(\\(none assigned\\))\"", name, "sub-tree"));
                findings.AddRange(Occurrences(json, "\"(recursion)\": \"[^\"]+\"", name, "sub-tree"));
            }

            return findings;
        }

        /// <summary>Runs <see cref="Verify"/> and logs the outcome. Returns true when everything is clean.</summary>
        public static bool VerifyAndLog(params string[] assetPaths)
        {
            var findings = Verify(assetPaths);

            if (findings.Count == 0)
            {
                Debug.Log($"[BehaviorTreeVerification] {assetPaths.Length} tree(s) verified clean after reload.");
                return true;
            }

            Debug.LogError(
                $"[BehaviorTreeVerification] {findings.Count} problem(s) after reload:\n  " +
                string.Join("\n  ", findings));

            return false;
        }

        /// <summary>
        /// Reports where a node's remembered parameter ports no longer match the branch they call.
        /// <para>
        /// The ports are declared from a copy of the sub-tree's contract, which is what keeps them independent
        /// of asset load order — so the copy going stale is the failure this has to catch. Renaming a required
        /// variable on a shared branch is otherwise invisible: every call site keeps its old port, still wired,
        /// still passing a value the branch no longer reads.
        /// </para>
        /// </summary>
        private static IEnumerable<string> ContractDrift(BehaviorTreeGraphAsset asset, string treeName)
        {
            foreach (var node in asset.graph.Nodes)
            {
                if (node is not RunBehaviorTreeGraphNode runNode) continue;

                foreach (var drift in runNode.DescribeContractDrift())
                {
                    yield return $"{treeName}: sub-tree contract — {drift}";
                }
            }
        }

        private static IEnumerable<string> UnsetPortsThatMatter(string json)
        {
            return Regex.Matches(json, "\"([A-Za-z_][A-Za-z0-9_]*)\": \"\\(unset\\)\"")
                .Select(match => match.Groups[1].Value)
                .Where(port => !PortsSafeToLeaveUnset.Contains(port))
                .Distinct();
        }

        private static IEnumerable<string> Occurrences(string json, string pattern, string treeName, string label)
        {
            return Regex.Matches(json, pattern)
                .Select(match => $"{treeName}: {label} — {match.Groups[1].Value}")
                .Distinct();
        }
    }
}

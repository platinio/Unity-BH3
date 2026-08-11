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
                findings.AddRange(LayoutDisagreeingWithPriority(asset, name));
                findings.AddRange(GuardProblems(asset, name));

                findings.AddRange(Occurrences(json, "\"error\": \"([^\"]+)\"", name, "node reported"));
                findings.AddRange(Occurrences(json, "\"note\": \"(nothing reaches or reads this node)\"", name, "orphan"));
                findings.AddRange(Occurrences(json, "\"subTree\": \"(\\(none assigned\\))\"", name, "sub-tree"));
                findings.AddRange(Occurrences(json, "\"(recursion)\": \"[^\"]+\"", name, "sub-tree"));
            }

            return findings;
        }

        /// <summary>
        /// What is wrong with this tree's guards. Five checks, all of them about failures that are silent at
        /// author time and expensive at runtime.
        /// </summary>
        private static IEnumerable<string> GuardProblems(BehaviorTreeGraphAsset asset, string name)
        {
            var findings = new List<string>();
            var graph = asset.graph;

            var childOfReactiveComposite = new HashSet<System.Guid>();

            foreach (var node in graph.Nodes)
            {
                // Selector is the only composite that reacts today. A Preempts guard anywhere else is
                // authored intent that silently does nothing.
                if (node is not Selector) continue;

                foreach (var child in graph.ChildrenInPriorityOrder(node))
                {
                    if (child != null) childOfReactiveComposite.Add(child.guid);
                }
            }

            foreach (var guard in graph.Nodes.OfType<ConditionalExecution>())
            {
                string label = $"{name}: guard '{guard.NodeName}'";
                var owner = guard.Owner;

                // 1. Impure guard. Tolerable when a guard only ran while its owner ran; not now. A reactive
                //    guard evaluates on its own schedule, including while unrelated branches execute, so a
                //    graph that caches what it found before answering would overwrite that value for the
                //    life of the agent. Warned rather than blocked -- a designer may have a reason.
                foreach (var writer in WriteUnitsInside(guard))
                {
                    findings.Add($"{label} writes a variable ('{writer}'). A guard must only read: it is "
                                 + "evaluated on its own schedule, including while other branches run.");
                }

                if (guard is not ReactiveGuard reactive)
                {
                    // 2. A conditional that looks like it was relied on for interruption. Its loss of
                    //    self-abort is the intended breaking change, and this is what makes the migration
                    //    checkable rather than remembered.
                    if (owner != null && childOfReactiveComposite.Contains(owner.guid))
                    {
                        findings.Add($"{label} is entry-only and gates a Selector branch. If it was there to "
                                     + "interrupt that branch, it no longer does -- make it a Reactive Guard.");
                    }

                    continue;
                }

                // 3. A reactive guard that can never become due watches nothing and re-checks nothing.
                foreach (var trigger in reactive.Triggers ?? (IReadOnlyList<GuardTrigger>)System.Array.Empty<GuardTrigger>())
                {
                    if (trigger is OnKeyChanged keys && (keys.Keys == null || keys.Keys.Count == 0))
                    {
                        findings.Add($"{label} watches no keys, so nothing can ever mark it dirty. Derive its "
                                     + "keys or give it an interval.");
                    }
                }

                // 4. No triggers at all means every tick. Legal, and sometimes right, but it is the most
                //    expensive thing a guard can do and it should be a choice rather than an oversight.
                if (reactive.Triggers == null || reactive.Triggers.Count == 0)
                {
                    findings.Add($"{label} has no triggers, so it re-checks every tick. Add an interval or a "
                                 + "key trigger, or an Every Frame trigger to say the cost is deliberate.");
                }

                // 5. Preemption is a Selector contract. Elsewhere the guard still gates entry and still
                //    aborts, but its bid is defined and not yet active.
                if (reactive.Preempts && owner != null && !childOfReactiveComposite.Contains(owner.guid))
                {
                    findings.Add($"{label} is set to preempt, but its owner is not a direct child of a "
                                 + "Selector. Preemption is defined there and not yet active anywhere else.");
                }
            }

            return findings;
        }

        /// <summary>
        /// Names of write units inside a guard's script graphs. The key-derivation walk already visits every
        /// unit, so spotting the ones that write is free.
        /// </summary>
        private static IEnumerable<string> WriteUnitsInside(ConditionalExecution guard)
        {
            var found = new List<string>();
            var assets = guard.scriptGraphAssets;

            if (assets == null) return found;

            foreach (var scriptGraph in assets)
            {
                if (scriptGraph?.graph == null) continue;

                foreach (var element in scriptGraph.graph.units)
                {
                    string unit = element?.GetType().Name;
                    if (string.IsNullOrEmpty(unit)) continue;

                    if (unit.Contains("SetVariable") || unit.Contains("SetBehaviorTreeVariable")) found.Add(unit);
                }
            }

            return found;
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

        /// <summary>
        /// Reports containers whose children read left-to-right in a different order than they run.
        /// <para>
        /// Execution order is the serialized transition index, so a layout that contradicts it is legal and
        /// runs correctly — which is exactly the problem. Anyone reviewing the canvas reads priority the way
        /// they always have, left to right, and gets the wrong answer with nothing on screen to say so unless
        /// they notice the badge. A warning rather than an error: generated trees can reasonably lay out
        /// however they like, and the fix is cosmetic.
        /// </para>
        /// </summary>
        private static IEnumerable<string> LayoutDisagreeingWithPriority(BehaviorTreeGraphAsset asset, string treeName)
        {
            foreach (var node in asset.graph.Nodes)
            {
                if (node is not ContainerNode) continue;

                var children = asset.graph.ChildrenInPriorityOrder(node);
                if (children.Count < 2) continue;

                for (int index = 1; index < children.Count; index++)
                {
                    if (children[index].Position.x >= children[index - 1].Position.x) continue;

                    // Carries the x coordinates because sibling branches very often share a node name — three
                    // Sequences under one Selector is the normal shape — and "runs 'Sequence' before
                    // 'Sequence'" tells the reader nothing about which two to go and look at.
                    yield return
                        $"{treeName}: layout ≠ priority — '{node.NodeName}' runs priority {index} " +
                        $"('{children[index - 1].NodeName}' at x={children[index - 1].Position.x:0}) before " +
                        $"priority {index + 1} ('{children[index].NodeName}' at " +
                        $"x={children[index].Position.x:0}), but lays them out the other way round.";

                    break;
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

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

                foreach (var port in UnsetPortsThatMatter(asset))
                {
                    findings.Add($"{name}: port '{port}' is unset and will throw when read.");
                }

                findings.AddRange(MissingNodeTypes(asset, name));
                findings.AddRange(ContractDrift(asset, name));
                findings.AddRange(LayoutDisagreeingWithPriority(asset, name));
                findings.AddRange(GuardProblems(asset, name));
                findings.AddRange(WatchedKeysWrittenUnobservably(asset, name));
                findings.AddRange(FunctionProblems(asset, name));
                findings.AddRange(InvalidConnections(asset, name));
                findings.AddRange(MisroutedTransitions(asset, name));

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
                // Selector is the only composite that reacts today. A TakesOverLowerPriority guard anywhere else is
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

                if (!guard.HasRecomputeSchedule)
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

                // What this guard's condition declares it depends on. Resolved once per guard: the same walk
                // the guard itself runs, so the report and the runtime agree by construction rather than by
                // two implementations of the same rule staying in step.
                var inherited = InheritedWatchedKeys.Resolve(guard);

                // 3. A reactive guard that can never become due watches nothing and re-checks nothing.
                foreach (var trigger in guard.Triggers)
                {
                    // Usable keys, not merely present ones: a blank entry is skipped at evaluation, so a
                    // list of blanks is a guard that watches nothing while looking like it watches something.
                    // Inherited keys count as watched: a trigger authored with none, whose condition is a
                    // Function, is a legitimate shape now — the keys arrive from the Function at evaluation.
                    if (trigger != null && trigger.Kind == GuardTriggerKind.OnKeyChanged
                        && trigger.UsableKeyCount() == 0 && inherited.Length == 0)
                    {
                        findings.Add($"{label} watches no keys, so nothing can ever mark it dirty. Derive its "
                                     + "keys or give it an interval.");
                    }
                }

                // 4. No triggers at all means every tick. Legal, and sometimes right, but it is the most
                //    expensive thing a guard can do and it should be a choice rather than an oversight.
                if (guard.Triggers.Count == 0)
                {
                    // A condition that declares its dependencies turns this from advice into an instruction:
                    // the keys are known, so the fix is exact. This is the one gap the authoring-time seed
                    // cannot close -- a Function assigned through the inspector runs no authoring code, and
                    // inheritance deliberately never invents a trigger where none exists, because that would
                    // make an existing guard evaluate less often than it does today.
                    findings.Add(inherited.Length > 0
                        ? $"{label} has no triggers, so it re-checks every tick even though its condition "
                          + $"declares watched keys ({string.Join(", ", inherited)}). Add a key trigger naming "
                          + "them, or re-create the guard with bt_guard_on_function, which seeds it."
                        : $"{label} has no triggers, so it re-checks every tick. Add an interval or a "
                          + "key trigger, or an Every Frame trigger to say the cost is deliberate.");
                }

                // 4b. The written-down schedule has fallen behind the condition (Unity-BH3#22). Runtime
                //     inheritance means the guard does wake on the new key, so this is not a behaviour bug --
                //     it is the asset describing a guard that no longer exists, which an author reads and
                //     believes. Only the understating direction is reported; a key the trigger lists that
                //     nothing declares cannot be told apart from a deliberate hand-typed one.
                foreach (var trigger in guard.Triggers)
                {
                    if (trigger == null || trigger.Kind != GuardTriggerKind.OnKeyChanged) continue;

                    foreach (var key in inherited)
                    {
                        if (string.IsNullOrWhiteSpace(key) || trigger.Keys.Contains(key)) continue;

                        findings.Add($"{label} does not list '{key}' among its watched keys, but its condition "
                                     + "declares it. The guard wakes on it at runtime, so the asset understates "
                                     + "what it watches. Refresh with bt_refresh_guard_keys.");
                    }
                }

                // 5. Preemption is a Selector contract. Elsewhere the guard still gates entry and still
                //    aborts, but its bid is defined and not yet active.
                if (guard.TakesOverLowerPriority && owner != null && !childOfReactiveComposite.Contains(owner.guid))
                {
                    findings.Add($"{label} is set to preempt, but its owner is not a direct child of a "
                                 + "Selector. Preemption is defined there and not yet active anywhere else.");
                }
            }

            return findings;
        }

        /// <summary>
        /// Keys a guard watches that something in this tree writes with Unity's stock <c>Set Variable</c>
        /// unit, which cannot bump a version — so the guard never wakes.
        ///
        /// <para>
        /// This is the silent failure the whole <c>OnKeyChanged</c> trigger rests on avoiding. A guard
        /// watching a key nothing observable ever writes is indistinguishable, at runtime, from a guard whose
        /// condition is simply false: no error, no warning, the branch just stops reacting. BH3's Set
        /// Behavior Tree Variable unit reports its writes; Unity's built-in one is the one a designer finds
        /// first in the fuzzy finder and cannot be hooked.
        /// </para>
        ///
        /// <para>
        /// Only writes <em>inside this tree</em> are visible here. A stock write from C# or from another
        /// asset is beyond reach and stays a documentation rule.
        /// </para>
        /// </summary>
        private static IEnumerable<string> WatchedKeysWrittenUnobservably(BehaviorTreeGraphAsset asset, string name)
        {
            var watched = WatchedKeys(asset);
            if (watched.Count == 0) return System.Array.Empty<string>();

            var findings = new List<string>();

            foreach (var node in asset.graph.Nodes)
            {
                foreach (var functionGraph in FunctionGraphsReadBy(node))
                {
                    foreach (var unit in functionGraph.units)
                    {
                        // The stock unit specifically. BH3's own writes through AgentVariableWriter and is
                        // fine, and it is not a subclass of this one, so an exact type test is right.
                        if (unit is not Unity.VisualScripting.SetVariable stock) continue;
                        if (stock.kind != Unity.VisualScripting.VariableKind.Object) continue;

                        string key = LiteralKeyOf(stock);

                        if (key == null || !watched.Contains(key)) continue;

                        findings.Add(
                            $"{name}: '{key}' is watched by a Reactive Guard but written by Unity's stock Set "
                            + $"Variable unit in '{functionGraph.title ?? "a Function"}', which cannot wake it. Use Set Behavior "
                            + "Tree Variable, or write it through AgentVariableWriter.");
                    }
                }
            }

            return findings;
        }

        /// <summary>Every agent key some reactive guard in this tree is watching.</summary>
        private static HashSet<string> WatchedKeys(BehaviorTreeGraphAsset asset)
        {
            var keys = new HashSet<string>();

            foreach (var guard in asset.graph.Nodes.OfType<ConditionalExecution>())
            {
                foreach (var trigger in guard.Triggers)
                {
                    if (trigger == null || trigger.Kind != GuardTriggerKind.OnKeyChanged || trigger.Keys == null) continue;

                    foreach (var key in trigger.Keys)
                    {
                        if (!string.IsNullOrWhiteSpace(key)) keys.Add(key);
                    }
                }
            }

            return keys;
        }

        /// <summary>
        /// The key a stock Set Variable writes, when it is knowable without running anything — an inline
        /// value on the port, or a literal wired into it. Null when the name is computed, which this check
        /// cannot and should not guess at.
        /// </summary>
        // Typed to the shared base rather than SetVariable: reading a variable's key and writing one are the
        // same lookup, and Function lints need it for GetVariable too.
        private static string LiteralKeyOf(Unity.VisualScripting.UnifiedVariableUnit stock)
        {
            var port = stock.name;
            if (port == null) return null;

            var connection = port.connection;

            if (connection?.source?.unit is Unity.VisualScripting.Literal literal)
            {
                return literal.value as string;
            }

            if (connection != null) return null;

            return stock.defaultValues != null && stock.defaultValues.TryGetValue(port.key, out var inline)
                ? inline as string
                : null;
        }

        /// <summary>
        /// The Functions a node reads, as graphs to walk.
        ///
        /// <para>
        /// These lints used to walk <c>scriptGraphAssets</c> — the embedded sub-assets a node owned. Nothing
        /// embeds any more, so that property answers empty and every lint reading it would have gone quiet
        /// without failing. A lint that silently stops checking is worse than one that is deleted, so both
        /// were re-pointed at where the graphs actually live.
        /// </para>
        /// </summary>
        private static IEnumerable<Unity.VisualScripting.FlowGraph> FunctionGraphsReadBy(BehaviorTreeNode node)
        {
            if (node is not BaseVisualScriptingNode holder) yield break;

            foreach (var slot in holder.GraphSlots)
            {
                var function = slot?.Function;
                if (function?.graph != null) yield return function.graph;
            }
        }

        /// <summary>
        /// Names of write units inside a guard's script graphs. The key-derivation walk already visits every
        /// unit, so spotting the ones that write is free.
        /// </summary>
        private static IEnumerable<string> WriteUnitsInside(ConditionalExecution guard)
        {
            var found = new List<string>();

            foreach (var functionGraph in FunctionGraphsReadBy(guard))
            {
                foreach (var element in functionGraph.units)
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
        /// <summary>
        /// What is wrong with the Functions this tree reads.
        /// <para>
        /// Every one of these used to be a runtime surprise or nothing at all. A query graph whose output key
        /// was misspelled failed when it ran and nowhere else; a guard condition that wrote had no way to be
        /// noticed; a watched-key list that was simply wrong produced a guard that never woke, silently. The
        /// point of a declared contract is that all of them become a name and a line here.
        /// </para>
        /// </summary>
        private static IEnumerable<string> FunctionProblems(BehaviorTreeGraphAsset asset, string treeName)
        {
            var reported = new HashSet<ArcaneOnyx.VisualScriptingExtension.FunctionGraphAsset>();

            foreach (var node in asset.graph.Nodes)
            {
                // A Script Graph node's four lifecycle slots take Functions, through the same picker the
                // value node uses. Two things can be wrong with one that nothing else catches: the slot
                // cannot declare ports, so a Function that needs an argument runs unfed; and OnUpdate's
                // Function is the node's verdict, so it has to return an ExecutionStatus. The requirement is
                // read off the slot by the same rule the picker filters with, so what verify names and what
                // the dropdown offers cannot disagree.
                if (node is VisualScriptingNode lifecycleNode)
                {
                    foreach (var slot in lifecycleNode.GraphSlots)
                    {
                        if (slot == null || !slot.ReadsFunction) continue;

                        var slotFunction = slot.Function;
                        var requirement = FunctionPortConstraint.For(slot);

                        if (!requirement.Satisfies(slotFunction.ResultType))
                        {
                            yield return
                                $"{treeName}: node '{lifecycleNode.NodeName}' assigns Function '{slotFunction.name}' " +
                                $"to a lifecycle slot requiring {requirement.Describe()}, but it returns " +
                                $"{slotFunction.ResultType?.Name ?? "nothing"}.";
                        }

                        foreach (var input in slotFunction.Inputs)
                        {
                            if (input.hasDefaultValue) continue;

                            yield return
                                $"{treeName}: node '{lifecycleNode.NodeName}' assigns Function '{slotFunction.name}' " +
                                $"to a lifecycle slot, which cannot declare ports, so its required input " +
                                $"'{input.key}' can never be supplied. Give it a default, or read the Function " +
                                "from a Script Graph Variable node instead.";
                        }
                    }
                }

                if (node is not VisualScriptGraphVariable variableNode) continue;

                // A node that reads nothing. Before embedding was retired this could only be reported as an
                // ambiguity — Function *and* embedded graph — because "no Function" was a legitimate state
                // meaning "reads the embedded one". Now it is simply broken, and this is what catches a tree
                // that arrives from an older version with its embedded graphs no longer readable.
                if (variableNode.Function == null)
                {
                    yield return
                        $"{treeName}: node '{variableNode.NodeName}' has no Function assigned, so it reads " +
                        "nothing. A tree written before embedded graphs were retired needs its reads " +
                        "re-authored as Functions.";

                    continue;
                }

                // Per node, not per Function: two nodes referencing one Function can be at different points
                // of staleness, and the one that has drifted is the one an author has to go and fix.
                foreach (var drift in variableNode.DescribeContractDrift())
                {
                    yield return
                        $"{treeName}: node '{variableNode.NodeName}' — Function contract {drift} " +
                        "Refresh its ports with fn_refresh_ports.";
                }

                var function = variableNode.Function;
                if (function == null) continue;

                // Checked per NODE, above the per-function dedupe below, because a mismatch is a property of
                // this node's wiring rather than of the Function: the same Function can be right on one node
                // and wrong on another.
                //
                // This is the drift case, and since step 2d it is the only case: the Output now takes the
                // Function's result type, so a non-fitting Function assigned to a wired node demotes the wire
                // (InvalidConnections below reports that). What the gate cannot see is a Function whose
                // Result changed AFTER the wire was drawn and the node not refreshed -- the port still
                // declares the old type, the wire is still valid, and the Function hands back something else.
                // Only comparing the live Result against the fed ports catches that.
                var constraint = FunctionPortConstraint.For(variableNode);

                if (function.ResultType != null && !constraint.Satisfies(function.ResultType))
                {
                    yield return
                        $"{treeName}: node '{variableNode.NodeName}' reads Function '{function.name}', which " +
                        $"returns {function.ResultType.Name}, but the node feeds {constraint.Describe()}.";
                }

                if (!reported.Add(function)) continue;

                var plan = ArcaneOnyx.VisualScriptingExtension.FunctionBindingPlan.Resolve(function);
                if (!plan.IsUsable)
                {
                    yield return $"{treeName}: Function — {plan.Error}";
                    continue;
                }

                // A missing result is not a resolve failure — the plan is perfectly usable for a Function run
                // for its control flow. It is only wrong for a Function something reads a value out of, which
                // is what a Script Graph Variable node does, so the check belongs here rather than in Resolve.
                // This is the "spelled exactly" failure: it used to surface when the graph ran and nowhere else.
                if (function.ResultType == null)
                {
                    yield return
                        $"{treeName}: Function '{function.name}' declares no " +
                        $"'{ArcaneOnyx.VisualScriptingExtension.FunctionGraphAsset.ResultKey}' output, so the " +
                        "node reading it has nothing to read.";
                }

                foreach (var problem in PurityAndWatchedKeys(function)) yield return $"{treeName}: {problem}";
            }
        }

        /// <summary>
        /// Compares what a Function declares against what its graph actually does. Declaration is what callers
        /// rely on; this walk is what keeps the declaration honest.
        /// </summary>
        private static IEnumerable<string> PurityAndWatchedKeys(
            ArcaneOnyx.VisualScriptingExtension.FunctionGraphAsset function)
        {
            if (function.graph == null) yield break;

            // The derivation lives on the asset, beside the declarations it checks, rather than here. Those
            // are one idea — what the author claims and what the graph does — and splitting them across
            // modules is how they drift.
            var readKeys = function.DeriveReadKeys();
            var writeUnits = function.DeriveWrites();

            // Warn rather than error, matching the choice spec 09 already made for guard purity: a Function
            // that writes is a design smell, not an impossibility, and the author may have meant it.
            if (function.Pure && writeUnits.Count > 0)
            {
                yield return
                    $"Function '{function.name}' is declared pure but writes: {string.Join(", ", writeUnits)}.";
            }

            foreach (var declared in function.WatchedKeys)
            {
                if (!readKeys.Contains(declared))
                {
                    yield return
                        $"Function '{function.name}' declares watched key '{declared}' but its graph never " +
                        "reads it.";
                }
            }

            foreach (var read in readKeys)
            {
                if (!function.WatchedKeys.Contains(read))
                {
                    yield return
                        $"Function '{function.name}' reads '{read}' but does not declare it as a watched key. " +
                        "A guard inheriting these keys will not wake when it changes.";
                }
            }
        }

        /// <summary>
        /// Wires the canvas draws red: connections whose ports no longer accept each other.
        ///
        /// <para>
        /// A connection is validated when it is drawn and then trusted, so one that stops fitting later is
        /// not removed -- <c>NodePreservation</c> demotes it to an invalid connection on the next
        /// <c>Define()</c>. The case that produces those now is a Script Graph Variable whose Output retyped
        /// when its Function changed: an <c>object</c> output that fed a Transform port becomes a <c>bool</c>
        /// output that cannot. The canvas shows it; this is what lets the CLI see the same thing, since the
        /// result-type lint above reads valid connections only and goes quiet the moment the wire is demoted.
        /// </para>
        /// </summary>
        private static IEnumerable<string> InvalidConnections(BehaviorTreeGraphAsset asset, string treeName)
        {
            foreach (var connection in asset.graph.invalidConnections)
            {
                if (!connection.sourceExists || !connection.destinationExists) continue;

                var source = connection.source;
                var destination = connection.destination;

                var from = source.behaviorTreeNode is BehaviorTreeNode sourceNode ? sourceNode.NodeName : "?";
                var to = destination.behaviorTreeNode is BehaviorTreeNode destinationNode ? destinationNode.NodeName : "?";

                // A ghost port is one NodePreservation recreated for a key the node no longer declares, so
                // that the wire had somewhere to stay attached. Named as such: "(control)" here would send
                // the reader looking for a flow port that does not exist.
                var sourceType = source is ValueOutput valueOutput ? valueOutput.Type.Name
                    : source is InvalidOutput ? "no longer declared" : "control";
                var destinationType = destination is ValueInput valueInput ? valueInput.Type.Name
                    : destination is InvalidInput ? "no longer declared" : "control";

                yield return
                    $"{treeName}: invalid connection -- '{from}'.{source.key} ({sourceType}) no longer fits " +
                    $"'{to}'.{destination.key} ({destinationType}). Rewire it, or change the Function so it " +
                    "fits again and the wire comes back by itself.";
            }
        }

        /// <summary>
        /// Nodes whose type no longer exists. The asset rewrites an unknown <c>$type</c> to
        /// <see cref="MissingType"/> on load, so the tree still opens and its wiring survives — which is
        /// exactly why nothing else notices: nothing throws, nothing is unset, the branch simply does
        /// nothing where that node was. Deleting a node type is the accepted way to retire one here, so
        /// the thing that names the leftovers has to exist.
        /// </summary>
        private static IEnumerable<string> MissingNodeTypes(BehaviorTreeGraphAsset asset, string treeName)
        {
            foreach (var node in asset.graph.Nodes)
            {
                if (node is not MissingType missing) continue;

                var former = string.IsNullOrEmpty(missing.formerType) ? "unknown" : missing.formerType;

                string fix = missing.HasPreservedState
                    ? $"It does nothing, but what the node held was kept: re-add its script, add "
                      + $"[RenamedFrom(\"{former}\")] to whatever replaced it, or run bt_retarget_missing "
                      + $"--former {former} --to <type>."
                    : "It does nothing, and nothing was preserved for it — replace it from the inspector, or "
                      + "delete it and rebuild what it did.";

                yield return
                    $"{treeName}: node at ({missing.Position.x:F0}, {missing.Position.y:F0}) has a type that " +
                    $"no longer exists (formerly '{former}'). {fix}";
            }
        }

        /// <summary>
        /// A transition whose destination refuses to be a child -- in practice a guard, dropped on because the
        /// canvas draws it exactly where a wire aimed at its owner lands. The runtime skips such a wire and
        /// says so at awake; this is the same fact at author time, before anything is played, and it names
        /// the wire rather than only the orphan it leaves behind.
        /// </summary>
        private static IEnumerable<string> MisroutedTransitions(BehaviorTreeGraphAsset asset, string treeName)
        {
            foreach (var transition in asset.graph.Transitions)
            {
                if (!BehaviorTreeGraph.EndsOnANodeThatCannotBeAChild(transition)) continue;

                yield return
                    $"{treeName}: {BehaviorTreeGraph.DescribeMisroutedTransition(transition)} Until then the " +
                    "runtime ignores it.";
            }
        }

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

        /// <summary>
        /// Ports that will throw when read, asked of the ports themselves.
        ///
        /// <para>
        /// This used to scrape <c>"(unset)"</c> out of the dump and drop any port whose <em>name</em> was in
        /// a hard-coded set of <c>{ "Animator", "Target" }</c>. Matching on name is matching on the wrong
        /// thing: it exempted every port called <c>Target</c> regardless of how its node read it, so
        /// <c>FaceTarget.Target</c> and any future node that named a genuinely-required port <c>Target</c>
        /// were silently excused — while a node reading an <c>Animator</c> through <c>GetValue</c> would have
        /// been excused too.
        /// </para>
        ///
        /// <para>
        /// Whether an unconnected port is a defect depends on how the node reads it, which only the node
        /// knows, so it is now declared at the port — see <c>ValueInput.SafeToLeaveUnconnected</c> — and the
        /// canvas badge asks the same question through the same property.
        /// </para>
        /// </summary>
        private static IEnumerable<string> UnsetPortsThatMatter(BehaviorTreeGraphAsset asset)
        {
            foreach (var node in asset.graph.Nodes)
            {
                if (node == null || !node.IsVisible) continue;

                foreach (var port in node.valueInputs)
                {
                    if (port == null || !port.IsUnfedRequired) continue;

                    yield return $"{node.NodeName}.{port.key}";
                }
            }
        }

        private static IEnumerable<string> Occurrences(string json, string pattern, string treeName, string label)
        {
            return Regex.Matches(json, pattern)
                .Select(match => $"{treeName}: {label} — {match.Groups[1].Value}")
                .Distinct();
        }
    }
}

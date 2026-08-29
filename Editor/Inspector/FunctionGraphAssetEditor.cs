using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The Function's own inspector: what it promises, what it actually does, and where those disagree.
    ///
    /// <para>
    /// Until this existed, the metadata that decides whether a guard ever wakes was a bare
    /// <c>List&lt;string&gt;</c> in the default inspector. A designer typed keys into it with no indication
    /// of what the graph reads, no check that a typed key was real, and no way to reconcile the two — while
    /// the derivation that answers all three had been sitting on the asset since the foundation pass, used
    /// only by <c>bt_verify</c>, which designers do not run.
    /// </para>
    ///
    /// <para>
    /// <b>The contract is shown, not edited.</b> A Function's inputs and outputs <em>are</em> its graph's
    /// port definitions — there is no second copy here to correct, and offering to edit them in two places
    /// is how they would come to disagree. What is actionable here is the metadata the graph cannot express
    /// (watched keys, purity) and the callers that hold a copy of the contract and can fall behind it.
    /// </para>
    ///
    /// <para>
    /// Lives in BH3's editor assembly rather than VisualScriptingExtension's for the reason the <c>fn_</c>
    /// commands do — and additionally because half of what it reports is about behavior tree callers, which
    /// VSE must not know about.
    /// </para>
    /// </summary>
    [CustomEditor(typeof(FunctionGraphAsset))]
    public class FunctionGraphAssetEditor : UnityEditor.Editor
    {
        private List<FunctionReferences.Caller> callers;
        private bool callersExpanded;

        private FunctionGraphAsset Function => (FunctionGraphAsset)target;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (Function.graph == null)
            {
                EditorGUILayout.HelpBox("This Function has no graph, so it cannot be evaluated.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space();
            DrawContract();

            EditorGUILayout.Space();
            DrawWatchedKeys();

            EditorGUILayout.Space();
            DrawPurity();

            EditorGUILayout.Space();
            DrawCallers();
        }

        // ------------------------------------------------------------------ contract

        private void DrawContract()
        {
            EditorGUILayout.LabelField("Contract", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                var inputs = FunctionParameter.ReadContract(Function);

                if (inputs.Count == 0) EditorGUILayout.LabelField("in", "(none)");

                foreach (var input in inputs)
                {
                    EditorGUILayout.LabelField(
                        "in",
                        $"{input.Name} : {input.Type?.Name ?? "?"}"
                        + (input.Optional ? $"   optional, default {input.DefaultValue}" : "   required"));
                }

                EditorGUILayout.LabelField("out", Function.ResultType != null
                    ? $"Result : {Function.ResultType.Name}"
                    : "(no Result declared — a node reading this has nothing to read)");

                DrawMisnamedResult();

                // The plan is what evaluation actually resolves against, so a Function can look complete here
                // and still refuse to run.
                var plan = FunctionBindingPlan.Resolve(Function);
                if (!plan.IsUsable) EditorGUILayout.HelpBox(plan.Error, MessageType.Error);
            }
        }

        /// <summary>
        /// The output-named-anything-but-Result mistake, said out loud with the repair a click away.
        ///
        /// <para>
        /// Callers read the output named <see cref="FunctionGraphAsset.ResultKey"/> specifically, and the
        /// graph editor lets an author name an output anything — so a Function whose one output is
        /// <c>SelectedPosition</c> evaluates fine, is offered by no picker, and nothing anywhere said why.
        /// A declared output under another name is evidence of intent to return something, which is what
        /// separates this from the legitimately void Function above (no outputs at all, run for its
        /// effects) that must stay unbothered.
        /// </para>
        /// </summary>
        private void DrawMisnamedResult()
        {
            if (Function.ResultType != null) return;

            var outputs = Function.Outputs.ToList();
            if (outputs.Count == 0) return;

            var resultKey = FunctionGraphAsset.ResultKey;

            if (outputs.Count > 1)
            {
                EditorGUILayout.HelpBox(
                    $"None of its {outputs.Count} outputs is named '{resultKey}', which is the one callers "
                    + "read — so a node reading this Function gets nothing, and no picker offers it. Rename "
                    + "whichever output is the result in the graph window.",
                    MessageType.Warning);
                return;
            }

            var lone = outputs[0];

            EditorGUILayout.HelpBox(
                $"Its output is named '{lone.key}', but callers read the output named '{resultKey}' — so a "
                + "node reading this Function gets nothing, and no picker offers it.",
                MessageType.Warning);

            if (GUILayout.Button($"Rename '{lone.key}' to '{resultKey}'"))
            {
                Undo.RecordObject(Function, "Rename Function output");
                FunctionGraphAuthoring.RenameOutput(Function, lone.key, resultKey);

                Debug.Log($"[BehaviorTree] {Function.name}: renamed output '{lone.key}' to '{resultKey}'.");
            }
        }

        // ------------------------------------------------------------------ watched keys

        private void DrawWatchedKeys()
        {
            EditorGUILayout.LabelField("Watched keys", EditorStyles.boldLabel);

            var read = Function.DeriveReadKeys() ?? new List<string>();
            var declared = Function.WatchedKeys;

            var undeclared = read.Where(key => !declared.Contains(key)).ToList();
            var unread = declared.Where(key => !read.Contains(key)).ToList();

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("declared", declared.Count == 0 ? "(none)" : string.Join(", ", declared));
                EditorGUILayout.LabelField("graph reads", read.Count == 0 ? "(none)" : string.Join(", ", read));
            }

            if (undeclared.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Read but not declared: {string.Join(", ", undeclared)}.\n\n"
                    + "Inheritance gives a guard the declaration, so no guard reading this Function wakes on "
                    + "these. Its branch can stop firing with nothing to point at.",
                    MessageType.Warning);

                if (GUILayout.Button($"Declare {undeclared.Count} key(s) the graph reads"))
                {
                    Apply(new List<string>(declared).Concat(undeclared).ToList(),
                        $"declared {string.Join(", ", undeclared)}");
                }
            }

            if (unread.Count > 0)
            {
                // Deliberately not offered as a one-click fix in the same breath as the one above. A key the
                // graph never reads may still be real: a variable name computed at runtime is invisible to
                // any walk, and the declaration is the only place such a dependency can be stated at all.
                EditorGUILayout.HelpBox(
                    $"Declared but never read by the graph: {string.Join(", ", unread)}.\n\n"
                    + "Harmless unless it is a typo — a misspelled key simply never fires. Keep it if it "
                    + "names something derived at runtime that no walk can see.",
                    MessageType.Info);

                if (GUILayout.Button($"Remove {unread.Count} key(s) the graph never reads")
                    && EditorUtility.DisplayDialog(
                        "Remove undeclared keys?",
                        $"Remove {string.Join(", ", unread)} from {Function.name}?\n\n"
                        + "Keep them if any names a variable this graph reads indirectly — through a name "
                        + "computed at runtime, for instance — because nothing can derive those.",
                        "Remove", "Cancel"))
                {
                    Apply(declared.Where(key => read.Contains(key)).ToList(),
                        $"removed {string.Join(", ", unread)}");
                }
            }

            if (undeclared.Count == 0 && unread.Count == 0 && read.Count > 0)
            {
                EditorGUILayout.HelpBox("The declaration matches what the graph reads.", MessageType.None);
            }
        }

        private void Apply(List<string> keys, string what)
        {
            Undo.RecordObject(Function, "Edit watched keys");
            Function.SetWatchedKeys(keys.ToArray());

            EditorUtility.SetDirty(Function);
            AssetDatabase.SaveAssets();

            // The declaration is what schedules guards, so live bindings and every canvas badge are stale.
            FunctionEvaluator.Invalidate(Function);
            NodeProblemCache.Invalidate();

            Debug.Log($"[BehaviorTree] {Function.name}: {what}.");
        }

        // ------------------------------------------------------------------ purity

        private void DrawPurity()
        {
            var writes = Function.DeriveWrites();
            if (!Function.Pure || writes.Count == 0) return;

            EditorGUILayout.HelpBox(
                $"Declared pure, but the graph writes: {string.Join(", ", writes)}.\n\n"
                + "A guard is evaluated on its own schedule, including while unrelated branches run, so a "
                + "condition that writes fires that side effect for the life of the agent.",
                MessageType.Warning);
        }

        // ------------------------------------------------------------------ callers

        private void DrawCallers()
        {
            EditorGUILayout.LabelField("Used by", EditorStyles.boldLabel);

            if (callers == null)
            {
                // Not scanned on every repaint: this loads every behavior tree in the project.
                if (GUILayout.Button("Find trees using this Function")) callers = FunctionReferences.Find(Function);
                return;
            }

            if (callers.Count == 0)
            {
                EditorGUILayout.HelpBox("No tree references this Function.", MessageType.Info);
                return;
            }

            var drifted = FunctionReferences.CountDrifted(callers);

            using (new EditorGUI.IndentLevelScope())
            {
                callersExpanded = EditorGUILayout.Foldout(callersExpanded, $"{callers.Count} tree(s)", true);

                if (callersExpanded)
                {
                    foreach (var caller in callers)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"{caller.Tree.name}  ({caller.Nodes.Count} node(s))");

                            if (GUILayout.Button("Select", GUILayout.Width(60.0f)))
                            {
                                Selection.activeObject = caller.Tree;
                                EditorGUIUtility.PingObject(caller.Tree);
                            }
                        }
                    }
                }
            }

            if (drifted == 0)
            {
                EditorGUILayout.HelpBox("Every caller's ports match this contract.", MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(
                $"{drifted} node(s) hold a copy of the contract that no longer matches. Their ports are out "
                + "of date until refreshed.",
                MessageType.Warning);

            if (GUILayout.Button($"Refresh ports on {drifted} node(s)")
                && EditorUtility.DisplayDialog(
                    "Refresh callers?",
                    $"Rebuild ports on {drifted} node(s) across {callers.Count} tree(s).\n\n"
                    + "Ports the contract no longer declares are removed, and whatever fed them is "
                    + "disconnected. Every dropped connection is named in the console.",
                    "Refresh", "Cancel"))
            {
                RefreshCallers();
            }
        }

        private void RefreshCallers()
        {
            var refreshed = 0;

            foreach (var caller in callers)
            {
                var touched = false;

                foreach (var node in caller.Nodes)
                {
                    if (node is not VisualScriptGraphVariable variable) continue;
                    if (variable.DescribeContractDrift().Count == 0) continue;

                    foreach (var lost in variable.RefreshParameters())
                    {
                        Debug.LogWarning($"[BehaviorTree] {caller.Tree.name}: {lost}");
                    }

                    ContractPortLayout.ResizeToFitPorts(variable);

                    refreshed++;
                    touched = true;
                }

                if (touched) EditorUtility.SetDirty(caller.Tree);
            }

            AssetDatabase.SaveAssets();
            NodeProblemCache.Invalidate();

            Debug.Log($"[BehaviorTree] Refreshed {refreshed} node(s) against {Function.name}.");

            callers = FunctionReferences.Find(Function);
        }
    }
}

using System;
using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Draws where a Script Graph Variable node gets its value, offering only the Functions that can legally
    /// go there.
    ///
    /// <para>
    /// <b>An <c>Inspector</c> rather than a <c>PropertyDrawer</c>, and that is forced rather than
    /// preferred.</b> Visual Scripting resolves an inspector per type and falls back to bridging a Unity
    /// <c>CustomPropertyDrawer</c> when no <c>Inspector</c> is registered. The bridge synthesises a host
    /// object — <c>SerializedPropertyProvider</c> — copies the value onto it, and hands the drawer a
    /// <c>SerializedProperty</c> belonging to that throwaway. So a drawer for this type can never see the
    /// owning node, and therefore never see <c>Output</c>, its connections, or the port type this picker
    /// filters on. A registered <c>Inspector</c> takes precedence over the bridge and receives
    /// <see cref="Metadata"/>, whose <c>parent</c> is the node. <see cref="GuardTriggerInspector"/> is the
    /// working precedent for the same shape.
    /// </para>
    ///
    /// <para>
    /// <b>This surface no longer creates embedded graphs.</b> The drawer it replaces minted a
    /// <c>ScriptGraphAsset</c> sub-asset whenever the node had nothing assigned, which is how most of the
    /// embedded graphs in this project came to exist — each one anonymous, owned by a garbage collector, and
    /// the reason a repository, an OnGUI sweep and an orphan lint all have to exist. A node with nothing
    /// assigned now gets a Function. One that already holds an embedded graph keeps opening it and gains
    /// <i>Extract to Function</i>, so nothing existing becomes unreachable. See spec 10, step 7.
    /// </para>
    /// </summary>
    [Inspector(typeof(BTScriptGraphVariable))]
    public class BTScriptGraphVariableInspector : Inspector
    {
        public BTScriptGraphVariableInspector(Metadata metadata) : base(metadata) { }

        /// <summary>
        /// How far up the metadata chain to look for the owning node. The field this inspector draws sits
        /// exactly one hop below it (verified against a live node), so anything beyond a couple of hops means
        /// this instance is drawing a <see cref="BTScriptGraphVariable"/> held somewhere else entirely — in
        /// which case there is no port to filter on and the picker says so rather than guessing.
        /// </summary>
        private const int MaxHopsToOwningNode = 4;

        private static float Row => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        private readonly AdvancedDropdownState dropdownState = new AdvancedDropdownState();

        private BTScriptGraphVariable Variable => metadata?.value as BTScriptGraphVariable;

        /// <summary>
        /// The node holding this variable, found by walking up rather than asked for: the value is a plain
        /// serialized member with no back-reference, and <c>metadata.parent</c> is the only route to it.
        /// </summary>
        private VisualScriptGraphVariable Node
        {
            get
            {
                var current = metadata?.parent;

                for (var hops = 0; current != null && hops < MaxHopsToOwningNode; hops++)
                {
                    if (current.value is VisualScriptGraphVariable node) return node;
                    current = current.parent;
                }

                return null;
            }
        }

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// What this inspector is going to draw, decided once so <see cref="GetHeight"/> and
        /// <see cref="OnGUI"/> cannot disagree. Two methods computing the same row list independently is the
        /// standard way an IMGUI inspector ends up drawing over the control below it.
        /// </summary>
        private struct Rows
        {
            public bool HasFunction;
            public bool HasEmbedded;
            public bool Ambiguous;
            public bool Mismatched;

            public int HelpBoxes => (Ambiguous ? 1 : 0) + (Mismatched ? 1 : 0);
            public bool HasButtons => HasFunction || HasEmbedded;
        }

        private Rows Describe()
        {
            var variable = Variable;
            var function = variable?.Function;

            var rows = new Rows
            {
                HasFunction = function != null,
                HasEmbedded = variable?.ScriptGraphAsset != null,
            };

            rows.Ambiguous = rows.HasFunction && rows.HasEmbedded;
            rows.Mismatched = rows.HasFunction && !FunctionPortConstraint.For(Node).Satisfies(function.ResultType);

            return rows;
        }

        protected override float GetHeight(float width, GUIContent label)
        {
            var rows = Describe();

            var height = Row;
            if (rows.HasButtons) height += Spacing + Row;
            height += rows.HelpBoxes * (Spacing + Row * 2.0f);

            return height;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            BeginLabeledBlock(metadata, position, label);

            var rows = Describe();
            var constraint = FunctionPortConstraint.For(Node);

            DrawPickerRow(position.VerticalSection(ref y, Row), label, constraint);

            if (rows.HasButtons)
            {
                y += Spacing;
                DrawButtonRow(position.VerticalSection(ref y, Row), rows);
            }

            if (rows.Mismatched)
            {
                y += Spacing;
                EditorGUI.HelpBox(
                    position.VerticalSection(ref y, Row * 2.0f),
                    $"'{Variable.Function.name}' returns "
                    + $"{Variable.Function.ResultType?.Name ?? "nothing"}, but this node feeds "
                    + $"{constraint.Describe()}. It is kept, not cleared — pick another or fix the Function.",
                    MessageType.Error);
            }

            if (rows.Ambiguous)
            {
                y += Spacing;
                EditorGUI.HelpBox(
                    position.VerticalSection(ref y, Row * 2.0f),
                    "The Function runs; the embedded graph is editable but dead. Clear one.",
                    MessageType.Warning);
            }

            EndBlock(metadata);
        }

        // ------------------------------------------------------------------ the picker

        private void DrawPickerRow(Rect row, GUIContent label, FunctionPortConstraint constraint)
        {
            var valueRect = EditorGUI.PrefixLabel(row, label);
            var function = Variable?.Function;

            var content = new GUIContent(
                function != null ? function.name : "None",
                function != null ? AssetDatabase.GetAssetPath(function) : DescribeWhatIsOffered(constraint));

            if (!EditorGUI.DropdownButton(valueRect, content, FocusType.Keyboard)) return;

            var dropdown = new FunctionDropdown(dropdownState, constraint, Assign, CreateAndAssign);
            dropdown.Show(valueRect);
        }

        private static string DescribeWhatIsOffered(FunctionPortConstraint constraint) =>
            constraint.IsUnconstrained
                ? "This node feeds nothing yet, so every Function is offered."
                : $"Only Functions whose result fits {constraint.Describe()} are offered.";

        // ------------------------------------------------------------------ the buttons

        private void DrawButtonRow(Rect row, Rows rows)
        {
            // An embedded graph gets two buttons — open it, or leave the embedded world — and a Function one.
            var count = rows.HasEmbedded ? (rows.HasFunction ? 3 : 2) : 1;
            var width = (row.width - Spacing * (count - 1)) / count;
            var next = row.x;

            Rect Slot()
            {
                var slot = new Rect(next, row.y, width, row.height);
                next += width + Spacing;
                return slot;
            }

            if (rows.HasFunction && GUI.Button(Slot(), "Open Function"))
            {
                OpenGraph(Variable.Function);
            }

            if (!rows.HasEmbedded) return;

            if (GUI.Button(Slot(), "Open Graph"))
            {
                OpenGraph(Variable.ScriptGraphAsset);
            }

            if (GUI.Button(Slot(), "Extract to Function"))
            {
                Extract();
            }
        }

        /// <summary>
        /// Opens whichever graph was asked for. Ported unchanged from the property drawer this replaces,
        /// minus the branch that created an embedded graph when there was none.
        /// </summary>
        private static void OpenGraph(UnityEngine.Object target)
        {
            if (target == null) return;

            GraphReference reference = null;

            if (target is IMacro macro)
                reference = GraphReference.New(macro, true);
            else if (target is IGraphRoot root)
                reference = GraphReference.New(root, false);

            if (target is IGraphNesterElement nesterElement)
                reference = LudiqGraphsEditorUtility.editedContext.value.reference.ChildReference(nesterElement, false);

            if (reference == null) return;

            GraphWindow.OpenActive(reference);
        }

        // ------------------------------------------------------------------ mutation

        /// <summary>
        /// Points the node at a Function and grows its ports.
        ///
        /// <para>
        /// Runs from a dropdown callback rather than from inside <c>OnGUI</c>, so <c>GUI.changed</c> is not
        /// the signal here — nothing is drawing. The repaint is asked for explicitly, and the problem cache
        /// is invalidated because the badge this assignment fixes or causes is otherwise computed at import.
        /// </para>
        /// </summary>
        private void Assign(FunctionGraphAsset function)
        {
            var node = Node;

            if (node == null)
            {
                // Reachable only if this type is ever inspected outside a live node's metadata chain. Said
                // out loud rather than returned silently: the author just picked from a full dropdown, and a
                // choice that vanishes without a word is the worst thing this surface could do.
                Debug.LogWarning(
                    "[BehaviorTree] The Function picker could not find the node it belongs to, so the "
                    + "selection was not applied.");
                return;
            }

            var label = function == null ? "Clear Function" : "Assign Function";
            var tree = RecordTreeEdit(label);

            // SetFunction refreshes the contract copy and re-declares the ports, which is what makes the
            // assignment usable without a second action.
            node.SetFunction(function);

            Authoring.ContractPortLayout.ResizeToFitPorts(node);
            Authoring.NodeProblemCache.Invalidate();

            MarkDirty(tree);

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// Registers an undo step against the tree asset and returns it, or null when no tree is being
        /// edited.
        ///
        /// <para>
        /// <b>Not <c>UndoUtility.RecordEditedObject</c>, and that is the whole point.</b> That helper reads
        /// the object to record from <c>LudiqEditorUtility.editedObject</c>, an override stack populated
        /// only inside <c>GraphContext.BeginEdit()/EndEdit()</c> — which brackets each canvas and inspector
        /// draw, frame by frame. Every mutation here runs from an <c>AdvancedDropdown</c> callback, raised
        /// by the dropdown's own popup window <em>outside</em> that bracket, where the stack reads null and
        /// the helper returns without recording or dirtying anything (<i>verified against the live
        /// editor</i>).
        /// </para>
        ///
        /// <para>
        /// The failure that causes is the quiet kind: <c>SetFunction</c> still mutates the in-memory node,
        /// so the assignment looks applied and survives the session — but the asset is never marked dirty,
        /// so it has no undo entry, never shows as unsaved, and is gone after a domain reload unless some
        /// unrelated edit happened to dirty the same tree. Naming the asset explicitly is what makes the
        /// edit real.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset RecordTreeEdit(string label)
        {
            var tree = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();

            if (tree == null)
            {
                Debug.LogWarning(
                    $"[BehaviorTree] '{label}' could not resolve the tree being edited, so the change has no "
                    + "undo entry and the asset was not marked dirty. Save the tree from the canvas.");
                return null;
            }

            Undo.RegisterCompleteObjectUndo(tree, label);
            return tree;
        }

        /// <summary>Marks the edited tree dirty after the mutation, so the change is actually saved.</summary>
        private static void MarkDirty(BehaviorTreeGraphAsset tree)
        {
            if (tree == null) return;

            EditorUtility.SetDirty(tree);
        }

        private void CreateAndAssign(FunctionPortConstraint constraint)
        {
            var function = CreateFunction(constraint);
            if (function == null) return;

            Assign(function);
            OpenGraph(function);
        }

        /// <summary>
        /// Creates a Function at a path the author picks, already declaring the result this port needs.
        ///
        /// <para>
        /// The result declaration is the point. <c>DefaultGraph()</c> cannot know what a Function is for and
        /// so declares Enter and Exit only; a Function created from a port does know, and one born without a
        /// <c>Result</c> would immediately fail the very filter that offered to create it.
        /// </para>
        /// </summary>
        private static FunctionGraphAsset CreateFunction(FunctionPortConstraint constraint)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Function",
                "New Function",
                "asset",
                "A Function is a project asset, so any tree can reference it.",
                DefaultFunctionFolder());

            if (string.IsNullOrEmpty(path)) return null;

            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();

            // Macro<TGraph> starts with an empty graph and nothing calls DefaultGraph() for a macro asset,
            // so a Function created without this has no input unit, no output unit and no Enter/Exit.
            function.graph = function.DefaultGraph();

            var resultType = constraint.SuggestedResultType ?? typeof(object);

            function.graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey,
                label = FunctionGraphAsset.ResultKey,
                type = resultType
            });

            function.graph.PortDefinitionsChanged();

            AssetDatabase.CreateAsset(function, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssets();

            return function;
        }

        /// <summary>Beside the tree being edited, which is where a Function made for it belongs.</summary>
        private static string DefaultFunctionFolder()
        {
            var tree = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();
            if (tree == null) return "Assets";

            var path = AssetDatabase.GetAssetPath(tree);
            if (string.IsNullOrEmpty(path)) return "Assets";

            return System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        }

        /// <summary>
        /// Promotes this node's embedded graph to a project asset and re-points the node at it — the one-way
        /// door out of the embedded world, and the same operation <c>fn_extract</c> performs from the CLI.
        /// </summary>
        private void Extract()
        {
            var node = Node;
            var tree = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();

            if (node == null || tree == null)
            {
                Debug.LogWarning(
                    "[BehaviorTree] Extract needs the tree that owns this node, and no tree is being edited.");
                return;
            }

            var suggested = node.EmbeddedScriptGraph == null || string.IsNullOrEmpty(node.EmbeddedScriptGraph.name)
                ? "New Function"
                : node.EmbeddedScriptGraph.name;

            var path = EditorUtility.SaveFilePanelInProject(
                "Extract to Function",
                suggested,
                "asset",
                "The embedded graph is copied into a Function this node then references.",
                DefaultFunctionFolder());

            if (string.IsNullOrEmpty(path)) return;

            RecordTreeEdit("Extract to Function");

            try
            {
                var function = Authoring.FunctionGraphAuthoring.ExtractToProjectAsset(tree, node, path);

                // The original sub-asset is deliberately left in place rather than destroyed here: deletion
                // has exactly one owner at a time (spec 10, locked decision 6), and bt_verify reports the
                // leftover as an orphan.
                Debug.Log(
                    $"[BehaviorTree] Extracted '{node.NodeName}' to '{AssetDatabase.GetAssetPath(function)}'. "
                    + "The embedded copy is still stored in the tree and is now unreferenced; bt_verify "
                    + "reports it as an orphan.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[BehaviorTree] Extract failed: {e.Message}");
                return;
            }

            Authoring.ContractPortLayout.ResizeToFitPorts(node);
            Authoring.NodeProblemCache.Invalidate();

            MarkDirty(tree);

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        // ------------------------------------------------------------------ the dropdown

        private sealed class FunctionDropdownItem : AdvancedDropdownItem
        {
            public readonly FunctionGraphAsset Function;
            public readonly bool CreatesNew;

            public FunctionDropdownItem(string name, FunctionGraphAsset function, bool createsNew = false)
                : base(name)
            {
                Function = function;
                CreatesNew = createsNew;
            }
        }

        /// <summary>
        /// The completion surface: at the port being filled, everything that can legally go there.
        ///
        /// <para>
        /// Unity's <c>AdvancedDropdown</c> rather than this project's <c>AdvancedDropdown</c> module, because
        /// BH3 is consumed as a tool and its editor assembly does not reference that module — a picker is not
        /// worth making every consumer take another submodule for. It also nests, so the flavor is the group
        /// an entry sits under rather than text repeated in every row.
        /// </para>
        /// </summary>
        private sealed class FunctionDropdown : AdvancedDropdown
        {
            private readonly FunctionPortConstraint constraint;
            private readonly Action<FunctionGraphAsset> assign;
            private readonly Action<FunctionPortConstraint> create;

            public FunctionDropdown(
                AdvancedDropdownState state,
                FunctionPortConstraint constraint,
                Action<FunctionGraphAsset> assign,
                Action<FunctionPortConstraint> create) : base(state)
            {
                this.constraint = constraint;
                this.assign = assign;
                this.create = create;

                minimumSize = new Vector2(320.0f, 320.0f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Function");

                root.AddChild(new FunctionDropdownItem("None", null));
                root.AddChild(new FunctionDropdownItem("Create new Function…", null, createsNew: true));
                root.AddSeparator();

                var offered = FunctionPickerCatalog.Offer(constraint);

                if (offered.Count == 0)
                {
                    root.AddChild(new AdvancedDropdownItem(NothingOffered()) { enabled = false });
                    return root;
                }

                var groups = new Dictionary<string, AdvancedDropdownItem>();

                foreach (var entry in offered)
                {
                    if (!groups.TryGetValue(entry.Group, out var group))
                    {
                        group = new AdvancedDropdownItem(entry.Group);
                        groups.Add(entry.Group, group);
                        root.AddChild(group);
                    }

                    group.AddChild(new FunctionDropdownItem(entry.Label, entry.Function));
                }

                return root;
            }

            /// <summary>
            /// Why the list is empty, which is a different sentence depending on the wiring. With one
            /// required type it means none exists yet; with several it can mean the node feeds ports that
            /// cannot be satisfied at once, and that is a fault in the wiring rather than in the project.
            /// </summary>
            private string NothingOffered()
            {
                if (constraint.IsUnconstrained) return "No Functions in this project yet";

                if (constraint.IsMultiplyConstrained && constraint.SuggestedResultType == null)
                {
                    return $"This node feeds {constraint.Describe()}, which no value can be at once";
                }

                return $"No Function returns {constraint.Describe()}";
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is not FunctionDropdownItem selected) return;

                if (selected.CreatesNew)
                {
                    create(constraint);
                    return;
                }

                assign(selected.Function);
            }
        }
    }
}

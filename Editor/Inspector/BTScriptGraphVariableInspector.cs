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
    /// <b>A slot holds a Function and nothing else.</b> The drawer this replaced minted a
    /// <c>ScriptGraphAsset</c> sub-asset whenever the node had nothing assigned, which is how most of the
    /// embedded graphs in this project came to exist — each one anonymous, owned by a garbage collector, and
    /// the reason a repository, an OnGUI sweep and an orphan lint all had to exist. Embedding is retired
    /// (spec 10, step 7); a slot with nothing assigned gets a Function from this picker.
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

        /// <summary>
        /// The object Visual Scripting is editing, captured while drawing.
        ///
        /// <para>
        /// Captured rather than looked up because it is only knowable <em>inside</em> <c>OnGUI</c>:
        /// <c>GraphContext.BeginEdit</c> pushes <c>reference.serializedObject</c> onto
        /// <c>LudiqEditorUtility.editedObject</c> for the duration of the draw and pops it afterwards. The
        /// dropdown's callback runs later, from its own popup window, where the stack is empty again.
        /// </para>
        ///
        /// <para>
        /// It is the asset for a macro graph and the <c>BehaviorTreeMachine</c> component for an embedded
        /// one, which is exactly the distinction that makes resolving the tree asset instead the wrong
        /// answer.
        /// </para>
        /// </summary>
        private UnityEngine.Object editedOwner;

        private BTScriptGraphVariable Variable => metadata?.value as BTScriptGraphVariable;

        /// <summary>
        /// The node holding this slot, found by walking up rather than asked for: the value is a plain
        /// serialized member with no back-reference, and <c>metadata.parent</c> is the only route to it.
        ///
        /// <para>
        /// Either kind of node. This used to look for a <see cref="VisualScriptGraphVariable"/> specifically,
        /// so on a Script Graph node -- whose four lifecycle slots draw with this same inspector -- it found
        /// nothing, and a pick from a full dropdown was dropped with a warning the author was not looking at.
        /// </para>
        /// </summary>
        private BaseVisualScriptingNode Owner
        {
            get
            {
                var current = metadata?.parent;

                for (var hops = 0; current != null && hops < MaxHopsToOwningNode; hops++)
                {
                    if (current.value is BaseVisualScriptingNode node) return node;
                    current = current.parent;
                }

                return null;
            }
        }

        /// <summary>The owner when it is the value-reading node, whose slot also owns ports. Null otherwise.</summary>
        private VisualScriptGraphVariable Node => Owner as VisualScriptGraphVariable;

        /// <summary>
        /// What may go in this slot. A value-reading node's requirement is read off the wiring of its
        /// Output; a lifecycle slot's is read off the slot itself, which knows whether it is run or read.
        /// </summary>
        private FunctionPortConstraint ConstraintForThisSlot() =>
            Node != null ? FunctionPortConstraint.For(Node) : FunctionPortConstraint.For(Variable);

        // ------------------------------------------------------------------ layout

        /// <summary>
        /// What this inspector is going to draw, decided once so <see cref="GetHeight"/> and
        /// <see cref="OnGUI"/> cannot disagree. Two methods computing the same row list independently is the
        /// standard way an IMGUI inspector ends up drawing over the control below it.
        ///
        /// <para>
        /// The help-box <em>texts</em> live here too, not just the flags, because a box's height is a
        /// function of its text and the width it is drawn at. Measuring one string in <c>GetHeight</c> and
        /// drawing another in <c>OnGUI</c> is the same disagreement in a different place.
        /// </para>
        /// </summary>
        private struct Rows
        {
            public bool HasFunction;

            /// <summary>The mismatch message, or null when the Function fits what the node feeds.</summary>
            public string Mismatch;

            public bool HasButtons => HasFunction;
        }

        private Rows Describe()
        {
            var variable = Variable;
            var function = variable?.Function;

            var rows = new Rows
            {
                HasFunction = function != null,
            };

            if (rows.HasFunction)
            {
                var constraint = ConstraintForThisSlot();

                if (!constraint.Satisfies(function.ResultType))
                {
                    rows.Mismatch =
                        $"'{function.name}' returns {function.ResultType?.Name ?? "nothing"}, but this " +
                        $"{(Node != null ? "node feeds" : "slot requires")} {constraint.Describe()}. It is " +
                        "kept, not cleared, pick another or fix the Function.";
                }
            }

            return rows;
        }

        protected override float GetHeight(float width, GUIContent label)
        {
            var rows = Describe();

            var height = Row;
            if (rows.HasButtons) height += Spacing + Row;

            // The boxes span the full block width, so this is exactly the width they are drawn at.
            if (rows.Mismatch != null) height += Spacing + HelpBoxes.HeightFor(rows.Mismatch, width, MessageType.Error);

            return height;
        }

        protected override void OnGUI(Rect position, GUIContent label)
        {
            // BeginLabeledBlock draws the label and hands back what is left of the row. Using that rect is
            // what keeps every row in the value column; drawing a second EditorGUI.PrefixLabel here instead
            // overlaid two labels sized by two different rules, which only looked right while they agreed.
            var content = BeginLabeledBlock(metadata, position, label);

            // Valid only inside this draw -- see the field's own note.
            editedOwner = LudiqEditorUtility.editedObject.value;

            var rows = Describe();
            var constraint = ConstraintForThisSlot();

            DrawPickerRow(content.VerticalSection(ref y, Row), constraint);

            if (rows.HasButtons)
            {
                y += Spacing;
                DrawButtonRow(content.VerticalSection(ref y, Row), rows);
            }

            // Help boxes take the whole block width rather than the value column: they are annotations on
            // the field, not values of it, and a message that has to explain two types and what to do about
            // them needs the room. It is also what makes the height measured in GetHeight the truth.
            if (rows.Mismatch != null)
            {
                y += Spacing;
                var height = HelpBoxes.HeightFor(rows.Mismatch, position.width, MessageType.Error);
                EditorGUI.HelpBox(position.VerticalSection(ref y, height), rows.Mismatch, MessageType.Error);
            }

            EndBlock(metadata);
        }

        // ------------------------------------------------------------------ the picker

        private void DrawPickerRow(Rect valueRect, FunctionPortConstraint constraint)
        {
            var function = Variable?.Function;

            var content = new GUIContent(
                function != null ? function.name : "None",
                function != null ? AssetDatabase.GetAssetPath(function) : DescribeWhatIsOffered(constraint));

            if (!EditorGUI.DropdownButton(valueRect, content, FocusType.Keyboard)) return;

            var dropdown = new FunctionDropdown(dropdownState, constraint, Assign, CreateAndAssign);
            dropdown.Show(valueRect);
        }

        private static string DescribeWhatIsOffered(FunctionPortConstraint constraint)
        {
            if (constraint.AllowsNoResult) return "This slot runs the Function for its effects, so every Function is offered.";
            if (constraint.IsUnconstrained) return "This node feeds nothing yet, so every Function is offered.";

            return $"Only Functions whose result fits {constraint.Describe()} are offered.";
        }

        // ------------------------------------------------------------------ the buttons

        private void DrawButtonRow(Rect row, Rows rows)
        {
            // One button. There used to be a row of up to three, because an embedded graph could be opened
            // or extracted to a Function, and a node could hold both at once.
            if (!rows.HasFunction) return;

            if (GUI.Button(row, "Open Function")) OpenGraph(Variable.Function);
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
        /// Points the node at a Function through <see cref="FunctionAssignment"/>, which owns the undo
        /// decision, and then repairs everything the editor caches about the node.
        ///
        /// <para>
        /// Runs from a dropdown callback rather than from inside <c>OnGUI</c>, so nothing here can rely on
        /// being mid-draw: <c>GUI.changed</c> reaches no one, the repaint is asked for explicitly, and the
        /// cached inspector height has to be invalidated by hand.
        /// </para>
        /// </summary>
        private void Assign(FunctionGraphAsset function)
        {
            var owner = Owner;

            if (owner == null)
            {
                // Reachable only if this type is ever inspected outside a live node's metadata chain. Said
                // out loud rather than returned silently: the author just picked from a full dropdown, and a
                // choice that vanishes without a word is the worst thing this surface could do.
                Debug.LogWarning(
                    "[BehaviorTree] The Function picker could not find the node it belongs to, so the "
                    + "selection was not applied.");
                return;
            }

            if (!FunctionAssignment.Apply(owner, Variable, function, editedOwner))
            {
                Debug.LogWarning(
                    "[BehaviorTree] The Function picker could not identify the asset or scene object that "
                    + "owns this tree, so the selection was not applied rather than applied without an undo "
                    + "entry. Reopen the tree from its asset or its Behavior Tree Machine and try again.");
                return;
            }

            AfterMutation();
        }

        /// <summary>
        /// What every mutation raised from a callback owes the editor afterwards.
        ///
        /// <para>
        /// <b>The height invalidation is the non-obvious one.</b> <c>Inspector</c> caches <c>GetHeight</c>
        /// and recomputes only when <c>isHeightDirty</c> is set or the height hash changes, and that hash
        /// covers width, label and wide mode, never the value. <c>isHeightDirty</c> comes from
        /// <c>metadata.valueChanged</c>, which fires only when the observed value stops being equal to the
        /// last one; <c>SetFunction</c> mutates the <em>same</em> <see cref="BTScriptGraphVariable"/>
        /// instance, so it is reference-equal before and after and nothing fires. Without this call, going
        /// from None to a Function leaves <c>GetHeight</c> reporting one row while <c>OnGUI</c> draws two or
        /// three, and the extra rows paint over the member below until something else resizes the panel.
        /// </para>
        /// </summary>
        private void AfterMutation()
        {
            SetHeightDirty();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void CreateAndAssign(FunctionPortConstraint constraint)
        {
            var function = CreateFunction(constraint);
            if (function == null) return;

            Assign(function);
            OpenGraph(function);
        }

        /// <summary>Where a Function made for this node belongs: beside the tree being edited.</summary>
        private static string DefaultFunctionFolder()
        {
            var tree = BehaviorTreeCanvas.GetBehaviorTreeGraphAsset();
            if (tree == null) return "Assets";

            var path = AssetDatabase.GetAssetPath(tree);
            if (string.IsNullOrEmpty(path)) return "Assets";

            var lastSlash = path.LastIndexOf('/');
            return lastSlash < 0 ? "Assets" : path.Substring(0, lastSlash);
        }

        /// <summary>
        /// Creates a Function at a path the author picks, already declaring the result this port needs.
        ///
        /// <para>
        /// The graph is built by <c>FunctionGraphAuthoring.CreateFunction</c> rather than here. "What does a
        /// new Function contain" is one decision, and it had three implementations before this parameter
        /// existed - the CLI path, the project-window create menu, and this picker - of which only the
        /// picker declared a <c>Result</c>. A Function created from the CLI was therefore born failing the
        /// very filter this picker applies, which is the UI/CLI asymmetry the feature set out to remove,
        /// reappearing on the create side.
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

            // The dialog has already asked about overwriting, so the answer given there is honoured rather
            // than quietly turned into "New Function 1" by GenerateUniqueAssetPath.
            // A side-effect slot gets a Function with no Result at all, which is what it wants; anywhere
            // else an unconstrained slot gets object, so an author who wires up afterwards still has a value.
            return Authoring.FunctionGraphAuthoring.CreateFunction(
                path, constraint.AllowsNoResult ? null : constraint.SuggestedResultType ?? typeof(object));
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

                // Impossibility is asked of Satisfies, not of SuggestedResultType. Those two answer
                // different questions on purpose, and using the stricter one here got the message wrong for
                // numeric pairs: a node feeding a float port and an int port has no assignable common type,
                // so SuggestedResultType is null -- but a float Function satisfies both, because float
                // converts to int. Telling that author to rewire when what they need is a float Function
                // sends them to fix something that is not broken.
                if (constraint.IsMultiplyConstrained && !AnyValueCouldSatisfy())
                {
                    return $"This node feeds {constraint.Describe()}, which no value can be at once";
                }

                return $"No Function returns {constraint.Describe()}";
            }

            /// <summary>
            /// Whether any type at all could fill every port at once, tested over the required types
            /// themselves plus <c>object</c>. Not a proof over all types -- there is no such enumeration --
            /// but it is exact for the case that matters, since a value filling several ports is in practice
            /// one of the types those ports declare.
            /// </summary>
            private bool AnyValueCouldSatisfy()
            {
                foreach (var required in constraint.RequiredTypes)
                {
                    if (constraint.Satisfies(required)) return true;
                }

                return constraint.Satisfies(typeof(object));
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

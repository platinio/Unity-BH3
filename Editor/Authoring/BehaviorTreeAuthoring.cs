using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

// StickyNote exists in both ArcaneOnyx.GraphCore and Unity.VisualScripting; the graph uses the GraphCore one
using StickyNote = ArcaneOnyx.GraphCore.StickyNote;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// The plumbing a generator needs and the tree API does not provide: creating tree assets on disk,
    /// parenting, guarding, and building the small Visual Scripting graphs that feed a guard.
    /// <para>
    /// Deliberately mechanical. Anything that decides what a tree *does* belongs in the generator that
    /// describes that tree, not here.
    /// </para>
    /// <para>
    /// Every method has a <c>bt_</c> CLI command beside it in <c>BehaviorTreeCommands</c>, so the same recipe
    /// runs from C# (one <c>eval_file</c> call, which is how a whole tree is best generated) or step by step
    /// over the Unity CLI. The C# methods take live nodes; the commands take an asset path and a node
    /// <c>guid</c> and resolve them, because each CLI call is a separate request that shares no state with
    /// the last one. The commands sit in their own assembly, compiled only when <c>com.unity.pipeline</c>
    /// is installed; nothing in this class needs the package.
    /// </para>
    /// </summary>
    public static class BehaviorTreeAuthoring
    {
        /// <summary>Creates an empty tree asset at <paramref name="assetPath"/>, replacing any existing one.</summary>
        public static BehaviorTreeGraphAsset CreateTree(string assetPath)
        {
            EnsureFolder(Path.GetDirectoryName(assetPath));

            var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);

            return asset;
        }

        /// <summary>
        /// Opens a tree that already exists, for editing in place. Everything else here works the same on a
        /// loaded asset as on a new one, so this is the entry point for adding to a tree rather than
        /// replacing it — <see cref="CreateTree"/> overwrites, and takes any hand-arranged layout with it.
        /// </summary>
        public static BehaviorTreeGraphAsset LoadTree(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(assetPath);
            if (asset == null) Debug.LogError($"[BehaviorTreeAuthoring] no behavior tree at {assetPath}.");

            return asset;
        }

        /// <summary>
        /// Adds a sticky note. Notes live in their own collection rather than among the nodes, so they never
        /// affect execution — including the child ordering that canvas X decides.
        /// <para>
        /// Replaces any existing note with the same title, so annotating a tree twice does not stack
        /// duplicates on top of each other.
        /// </para>
        /// </summary>
        public static StickyNote AddSticky(
            BehaviorTreeGraphAsset asset, string title, string body, float x, float y,
            float width = 300.0f, float height = 150.0f,
            StickyNote.ColorEnum color = StickyNote.ColorEnum.Classic)
        {
            foreach (var existing in asset.graph.Sticky.Where(note => note.title == title).ToList())
            {
                asset.graph.Sticky.Remove(existing);
            }

            var sticky = new StickyNote
            {
                position = new Rect(x, y, width, height),
                title = title,
                body = body,
                colorTheme = color
            };

            asset.graph.Sticky.Add(sticky);

            return sticky;
        }

        /// <summary>Which of a tree's three declaration lists <see cref="Declare"/> writes to.</summary>
        public enum DeclarationScope
        {
            /// <summary>The tree's own variables. Overwritten from the agent at runtime.</summary>
            Instance,

            /// <summary>What the agent must supply. Never read at runtime — the contract, for checking.</summary>
            Required,

            /// <summary>What this tree defaults to when the agent supplies nothing. The agent still wins.</summary>
            Optional
        }

        /// <summary>
        /// Declares a variable on the tree. At runtime <c>BehaviorTreeMachine.OverrideGraphVariables</c>
        /// overwrites <see cref="DeclarationScope.Instance"/> from the agent's Variables component, and every
        /// read resolves against the root tree — so on a sub-tree an Instance declaration supplies nothing.
        /// Use <see cref="DeclarationScope.Required"/> to state what the agent owes this branch, and
        /// <see cref="DeclarationScope.Optional"/> to give the branch a default it can carry on its own.
        /// </summary>
        public static void Declare(BehaviorTreeGraphAsset asset, string name, object value,
            DeclarationScope scope = DeclarationScope.Instance)
        {
            Declarations(asset, scope).Set(name, value);
        }

        private static VariableDeclarations Declarations(BehaviorTreeGraphAsset asset, DeclarationScope scope)
        {
            return scope switch
            {
                DeclarationScope.Required => asset.requiredDeclarations,
                DeclarationScope.Optional => asset.optionalDeclarations,
                _ => asset.declarations
            };
        }

        /// <summary>
        /// Adds a node at a canvas position, sized the way the editor sizes it.
        /// <para>
        /// The size comes from the node's own <c>StartingSize</c> rather than from the caller — the same
        /// thing <c>BaseCanvas.CreateGraphElementWithType</c> does when a node is made from the create menu.
        /// A node whose rect does not match what it was drawn for renders wrong: too wide and the icon
        /// overlaps the title, too short and the ports crowd.
        /// </para>
        /// </summary>
        public static T AddNode<T>(BehaviorTreeGraphAsset asset, float x, float y) where T : BehaviorTreeNode, new()
        {
            return AddNode<T>(asset.graph, x, y);
        }

        /// <summary>
        /// The same, on a graph with no asset behind it: a tree assembled in memory for a test, or a branch
        /// built before it is embedded anywhere. How a node is created and sized is decided here once; the
        /// asset overload only says which graph.
        /// </summary>
        public static T AddNode<T>(BehaviorTreeGraph graph, float x, float y) where T : BehaviorTreeNode, new()
        {
            var node = new T();
            node.Position = new Rect(new Vector2(x, y), node.StartingSize);

            // Nodes.Add fires AfterAdd -> Define(), so ports exist as soon as this returns
            graph.Nodes.Add(node);

            return node;
        }

        /// <summary>
        /// Parents <paramref name="child"/> under <paramref name="parent"/> at a given priority.
        /// <para>
        /// <paramref name="index"/> <em>is</em> the execution order — 0 is the branch tried first. Left unset,
        /// the child takes the next free slot, so making these calls in the order you want the branches tried
        /// produces the right tree. Canvas position no longer decides execution order; lay children out left
        /// to right anyway so the picture agrees with the priorities.
        /// </para>
        /// </summary>
        public static void Connect(BehaviorTreeGraphAsset asset, BehaviorTreeNode parent, BehaviorTreeNode child, int index = -1)
        {
            Connect(asset.graph, parent, child, index);
        }

        /// <summary>The same, on a graph with no asset behind it. See <see cref="AddNode{T}(BehaviorTreeGraph, float, float)"/>.</summary>
        public static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child, int index = -1)
        {
            if (index < 0) index = graph.ChildTransitionsInPriorityOrder(parent).Count;

            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, index);
            graph.Transitions.Add(transition);
        }

        /// <summary>
        /// Guards <paramref name="owner"/> on a boolean read from an agent variable. Guards are re-evaluated
        /// every tick while the owner runs, and several may name the same owner — they are ANDed.
        /// <para>
        /// The read goes through a Visual Scripting graph rather than the Get Variable node so it can carry a
        /// fallback: a branch dropped into an agent that never declares <paramref name="variableName"/> then
        /// behaves as <paramref name="fallback"/> instead of throwing "Variable not found".
        /// </para>
        /// </summary>
        /// <summary>Which kind of guard <see cref="GuardOnVariable"/> builds.</summary>
        public enum GuardKind
        {
            /// <summary>Keeps watching: aborts its branch and can preempt a lower-priority one.</summary>
            Reactive,

            /// <summary>Checks once at entry and stops caring.</summary>
            Conditional,
        }

        /// <summary>
        /// A guard reading an agent variable, with a fallback, negated when <paramref name="expected"/> is false.
        ///
        /// <para>
        /// <b>Defaults to <see cref="GuardKind.Reactive"/>, which is a deliberate divergence from the spec's
        /// stated default of a plain conditional.</b> Every existing caller of this helper wrote it meaning
        /// "guard this branch" back when a guard interrupted by definition — so preserving the <em>type</em>
        /// would silently strip interruption from every code-generated tree in the project, while preserving
        /// the <em>behaviour</em> keeps them doing what their authors asked for. A caller that wants the
        /// entry-only doorman is making the rarer and more deliberate choice, and now has to say so.
        /// </para>
        /// </summary>
        public static ConditionalExecution GuardOnVariable(
            BehaviorTreeGraphAsset asset, BehaviorTreeNode owner, string variableName, bool expected, bool fallback,
            float x, float y, GuardKind kind = GuardKind.Reactive)
        {
            // A blank name produces a guard that reads nothing and, as a reactive guard, watches nothing --
            // so it never wakes and never reports, which is the worst failure this feature has. Refused here
            // rather than left for someone to find in play mode.
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new System.ArgumentException(
                    "A guard needs a variable name to read; a blank one can never become true or wake.",
                    nameof(variableName));
            }

            var read = AddNode<VisualScriptGraphVariable>(asset, x, y + 90.0f);
            read.SetFunction(CreateVariableReadFunction(asset, variableName, fallback));
            SetComment(read, (expected ? "" : "not ") + variableName);

            ConditionalExecution guard;
            ArcaneOnyx.BehaviorTree.ValueInput value;

            if (kind == GuardKind.Conditional)
            {
                var doorman = AddNode<BooleanConditionalExecution>(asset, x, y);
                guard = doorman;
                value = doorman.Value;
            }
            else
            {
                var watchman = AddNode<BooleanReactiveGuard>(asset, x, y);

                // The key is derivable here without walking anything: this helper was handed the variable
                // name. Deriving it at the one place that already knows it is cheaper and more reliable than
                // recovering it from the graph afterwards.
                watchman.AddTrigger(GuardTrigger.KeyChanged(variableName));

                guard = watchman;
                value = watchman.Value;
            }

            guard.UpdateOwner(owner);

            if (expected)
            {
                read.Output.ValidlyConnectTo(value);
            }
            else
            {
                var not = AddNode<Not>(asset, x, y + 190.0f);
                read.Output.ValidlyConnectTo(not.Value);
                not.Result.ValidlyConnectTo(value);
            }

            return guard;
        }

        /// <summary>
        /// A guard whose condition is a Function, with its watch schedule seeded from what that Function
        /// declares — the counterpart of <see cref="GuardOnVariable"/> for a named, shared predicate.
        ///
        /// <para>
        /// <b>Seeding happens here for the same reason it happens there: this is the one place that already
        /// knows the answer.</b> The Function's declared keys are exactly the facts that can move its result,
        /// so the trigger they imply is written into the asset now, where an author can see it on the node and
        /// edit it, rather than conjured at runtime where nobody can find it. A guard that later drifts from
        /// its Function is repaired without a refresh step — the keys are re-read live on evaluation (see
        /// <c>InheritedWatchedKeys</c>) — so this seed is a starting schedule, not a copy anybody has to
        /// maintain.
        /// </para>
        ///
        /// <para>
        /// <b>A Function declaring no keys gets no trigger, deliberately.</b> That is not an omission to fix
        /// by guessing an interval: it means the Function claims no agent-fact dependency, so there is no key
        /// schedule to give it and every-tick remains the only honest default. If the claim is wrong,
        /// <c>bt_verify</c>'s existing watched-keys lint already catches it by comparing the declaration
        /// against what the graph actually reads.
        /// </para>
        /// </summary>
        public static ConditionalExecution GuardOnFunction(
            BehaviorTreeGraphAsset asset, BehaviorTreeNode owner, FunctionGraphAsset function, bool expected,
            float x, float y, GuardKind kind = GuardKind.Reactive)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));

            // Refused at authoring rather than at runtime: a non-boolean Function wired to a guard throws
            // inside Evaluate on the first tick, which surfaces as a broken tree rather than as the naming
            // mistake it actually is.
            if (!function.IsPredicate)
            {
                throw new ArgumentException(
                    $"Function '{function.name}' returns " +
                    $"{(function.ResultType == null ? "no '" + FunctionGraphAsset.ResultKey + "' output" : function.ResultType.Name)}, " +
                    "so it cannot be a guard condition. A guard needs a Function whose Result is bool.",
                    nameof(function));
            }

            var read = AddNode<VisualScriptGraphVariable>(asset, x, y + 90.0f);
            read.SetFunction(function);
            SetComment(read, (expected ? "" : "not ") + function.name);

            ConditionalExecution guard;
            ArcaneOnyx.BehaviorTree.ValueInput value;

            if (kind == GuardKind.Conditional)
            {
                var doorman = AddNode<BooleanConditionalExecution>(asset, x, y);
                guard = doorman;
                value = doorman.Value;
            }
            else
            {
                var watchman = AddNode<BooleanReactiveGuard>(asset, x, y);
                guard = watchman;
                value = watchman.Value;
            }

            guard.UpdateOwner(owner);

            if (expected)
            {
                read.Output.ValidlyConnectTo(value);
            }
            else
            {
                var not = AddNode<Not>(asset, x, y + 190.0f);
                read.Output.ValidlyConnectTo(not.Value);
                not.Result.ValidlyConnectTo(value);
            }

            // Seeded after wiring, and from the walk the guard itself runs rather than from the asset's list
            // directly. Both matter: before the connection exists there is nothing to walk, and reading the
            // declaration here instead would put a second implementation of "how a declared key list is
            // normalised" beside the first, free to disagree about a duplicate or a padded entry. Seeding
            // through the walk makes the written schedule provably what the runtime would have inherited.
            if (guard is ReactiveGuard watchmanGuard)
            {
                var declared = InheritedWatchedKeys.Resolve(watchmanGuard);
                if (declared.Length > 0) watchmanGuard.AddTrigger(GuardTrigger.KeyChanged(declared));
            }

            return guard;
        }

        /// <summary>
        /// A Function that reads one Object variable off the agent and returns it, falling back when the
        /// agent does not declare it.
        ///
        /// <para>
        /// <b>A named project asset, beside the tree that asked for it</b>, at
        /// <c>&lt;TreeFolder&gt;/Functions/&lt;Tree&gt;.&lt;variable&gt;Read.asset</c>. This used to mint an
        /// anonymous sub-asset of the tree, which is what made these graphs findable by nobody and
        /// deletable only by a garbage collector. The cost is the one spec 10 named and accepted: a tree
        /// that used to be self-contained now depends on a folder of small Functions.
        /// </para>
        ///
        /// <para>
        /// Reusing an existing Function at the same path is deliberate. Two guards in one tree reading
        /// <c>hp</c> asked for two identical sub-assets before; now they share one asset, which is what a
        /// named, shared thing is for.
        /// </para>
        ///
        /// <para>
        /// <b>The first caller's fallback wins, and a second caller asking for a different one is told.</b>
        /// The path is keyed on the variable, not on the fallback, so the two callers want one asset — but
        /// silently handing back a Function whose fallback is not the one just requested is the kind of
        /// thing that costs an afternoon six months later. Warned rather than refused: the reuse is the
        /// wanted behaviour, and refusing would make the second guard impossible to author at all.
        /// </para>
        /// </summary>
        public static FunctionGraphAsset CreateVariableReadFunction(
            BehaviorTreeGraphAsset owner, string variableName, object fallback)
        {
            var path = VariableReadFunctionPath(owner, variableName);

            var existing = AssetDatabase.LoadAssetAtPath<FunctionGraphAsset>(path);
            if (existing != null)
            {
                WarnIfFallbackDiffers(existing, variableName, fallback);
                return existing;
            }

            EnsureFunctionsFolder(owner);

            // The skeleton -- input unit, output unit, Enter/Exit/Result -- comes from the one place that
            // owns what a new Function contains rather than being assembled here for a third time. That
            // duplication is what produced the create-menu bug, where a second copy of this made an asset
            // with no Enter and nothing to run.
            var function = FunctionGraphAuthoring.CreateFunction(path, typeof(object));
            var graph = function.graph;

            var input = graph.units.OfType<ScriptGraphInput>().First();
            var output = graph.units.OfType<ScriptGraphOutput>().First();

            // specifyFallback is read by Definition(), so it has to be set before the unit joins the graph
            var getVariable = new Unity.VisualScripting.GetVariable
            {
                kind = VariableKind.Object,
                specifyFallback = true,
                position = new Vector2(-120.0f, 0.0f)
            };
            graph.units.Add(getVariable);

            getVariable.name.SetDefaultValue(variableName);

            // fallback is declared as ValueInput<object>, and object is not a type Visual Scripting can store
            // inline, so SetDefaultValue on it is a silent no-op and the port would read as unset
            var fallbackLiteral = new Unity.VisualScripting.Literal(fallback.GetType(), fallback)
            {
                position = new Vector2(-320.0f, 140.0f)
            };
            graph.units.Add(fallbackLiteral);
            fallbackLiteral.output.ValidlyConnectTo(getVariable.fallback);

            // Enter is already wired to Exit by the default graph; only the Result needs feeding.
            getVariable.value.ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);

            // Derived rather than defaulted, for the same reason the migration derives them: this graph
            // reads exactly one key, and a guard whose condition is this Function inherits that key to
            // decide when it may recompute. Leaving the list empty is the stale-guard failure.
            function.SetWatchedKeys(new[] { variableName });

            EditorUtility.SetDirty(function);
            AssetDatabase.SaveAssets();

            FunctionEvaluator.Invalidate(function);

            return function;
        }

        /// <summary>Where <see cref="CreateVariableReadFunction"/> puts what it makes.</summary>
        public static string VariableReadFunctionPath(BehaviorTreeGraphAsset owner, string variableName)
        {
            var treePath = AssetDatabase.GetAssetPath(owner);
            var folder = System.IO.Path.GetDirectoryName(treePath)?.Replace('\\', '/') ?? "Assets";
            var treeName = System.IO.Path.GetFileNameWithoutExtension(treePath);

            return $"{folder}/Functions/{treeName}.{FileSafe(variableName)}Read.asset";
        }

        /// <summary>
        /// A variable key as a file name. An agent variable may legally contain characters a path may not —
        /// <c>CreateAsset</c> then fails with Unity's message about the path rather than ours about the key,
        /// which sends the reader to the wrong problem.
        /// </summary>
        private static string FileSafe(string variableName)
        {
            var kept = new System.Text.StringBuilder(variableName.Length);

            foreach (var character in variableName)
            {
                var invalid = System.Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(), character) >= 0;
                kept.Append(invalid || character == '.' ? '_' : character);
            }

            return kept.Length == 0 ? "Variable" : kept.ToString();
        }

        /// <summary>
        /// Says so when a caller asks for a variable read that already exists with a different fallback.
        /// See <see cref="CreateVariableReadFunction"/> for why this warns rather than refusing.
        /// </summary>
        private static void WarnIfFallbackDiffers(FunctionGraphAsset existing, string variableName, object fallback)
        {
            var literal = existing.graph?.units.OfType<Unity.VisualScripting.Literal>().FirstOrDefault();
            if (literal == null || Equals(literal.value, fallback)) return;

            Debug.LogWarning(
                $"[BehaviorTree] '{existing.name}' already reads '{variableName}' with a fallback of "
                + $"'{literal.value}', so the requested fallback of '{fallback}' was not applied. One "
                + "Function serves every read of this variable in this tree; edit the asset to change it.");
        }

        private static void EnsureFunctionsFolder(BehaviorTreeGraphAsset owner)
        {
            var treePath = AssetDatabase.GetAssetPath(owner);
            var folder = System.IO.Path.GetDirectoryName(treePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return;

            var functions = folder + "/Functions";
            if (!AssetDatabase.IsValidFolder(functions)) AssetDatabase.CreateFolder(folder, "Functions");
        }

        /// <summary>
        /// Gives a port a value, by whichever mechanism actually works for that port.
        /// <para>
        /// Prefer this over <c>SetDefaultValue</c> everywhere. An inline value only survives serialization on
        /// a port whose <c>Definition()</c> declared one — <c>defaultValues</c> is cleared by
        /// <c>Undefine()</c> and rebuilt from <c>Definition()</c>, so a key added to a bare port is gone by
        /// the time the asset reloads, and the port then throws on first tick. This checks which case the
        /// port is and routes to an inline value or a connected literal accordingly.
        /// </para>
        /// </summary>
        public static void SetValue(BehaviorTreeGraphAsset asset, ValueInput port, object value, float x, float y)
        {
            var owner = port.behaviorTreeNode as BehaviorTreeNode;

            if (owner == null)
            {
                Debug.LogError($"[BehaviorTreeAuthoring] port '{port.key}' has no owning node; add the node to the graph first.");
                return;
            }

            // Definition() has already run by now, so a declared default is present as a key
            if (owner.defaultValues.ContainsKey(port.key))
            {
                port.SetDefaultValue(value);
                return;
            }

            switch (value)
            {
                case float f: FeedLiteral<FloatLiteral>(asset, port, f, x, y, l => l.Value); return;
                case int i: FeedLiteral<IntegerLiteral>(asset, port, i, x, y, l => l.Value); return;
                case bool b: FeedLiteral<BoolLiteral>(asset, port, b, x, y, l => l.Value); return;
                case string s: FeedLiteral<StringLiteral>(asset, port, s, x, y, l => l.Value); return;
                case Vector2 v2: FeedLiteral<Vector2Literal>(asset, port, v2, x, y, l => l.Value); return;
                case Vector3 v3: FeedLiteral<Vector3Literal>(asset, port, v3, x, y, l => l.Value); return;
            }

            Debug.LogError(
                $"[BehaviorTreeAuthoring] {owner.GetType().Name}.{port.key} declares no default, so it needs a " +
                $"connection, and there is no literal node for {value?.GetType().Name ?? "null"}. Wire it by hand.");
        }

        private static void FeedLiteral<T>(
            BehaviorTreeGraphAsset asset, ValueInput port, object value, float x, float y,
            System.Func<T, ValueOutput> output) where T : BehaviorTreeNode, new()
        {
            var literal = AddNode<T>(asset, x, y);
            SetPrivateField(literal, "value", value);
            output(literal).ValidlyConnectTo(port);
        }

        /// <summary>
        /// Feeds a float port from a literal node.
        /// <para>
        /// Use this, not <c>SetDefaultValue</c>, for any port whose <c>Definition()</c> declares no default.
        /// An inline value only survives serialization on ports that were declared with one — on the rest the
        /// value is silently gone by the time the asset reloads, and the port reads as unset and throws.
        /// </para>
        /// </para>
        /// </summary>
        public static void FeedFloat(BehaviorTreeGraphAsset asset, ValueInput port, float value, float x, float y)
        {
            var literal = AddNode<FloatLiteral>(asset, x, y);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        /// <summary>Feeds a Vector3 port from a literal node. See <see cref="FeedFloat"/> for why.</summary>
        public static void FeedVector3(BehaviorTreeGraphAsset asset, ValueInput port, Vector3 value, float x, float y)
        {
            var literal = AddNode<Vector3Literal>(asset, x, y);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        /// <summary>Runs another tree as a child. The guard for it belongs here, not inside the sub-tree.</summary>
        public static RunBehaviorTreeGraphNode AddSubTree(
            BehaviorTreeGraphAsset asset, BehaviorTreeGraphAsset subTree, float x, float y)
        {
            var node = AddNode<RunBehaviorTreeGraphNode>(asset, x, y);
            node.SetBehaviorTreeGraphAsset(subTree);

            return node;
        }

        public static void Save(BehaviorTreeGraphAsset asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        /// <summary>The node comment is what the canvas shows as a title, so it is how a generated node stays readable.</summary>
        public static void SetComment(BehaviorTreeNode node, string comment)
        {
            SetPrivateField(node, "comment", comment);
        }

        /// <summary>
        /// Literal nodes keep their value in a private serialized field with no setter, so authoring one from
        /// code means reaching for it. Walks the hierarchy so a field declared on a base class is reachable.
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();

            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Debug.LogWarning($"[BehaviorTreeAuthoring] no field '{fieldName}' on {target.GetType().Name}.");
        }

        private static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>
        /// Gives every reactive guard in <paramref name="asset"/> that has <b>no schedule at all</b>, and
        /// whose condition declares watched keys, a key trigger seeded from those keys. Returns the guards it
        /// changed, so a caller can report what it did rather than claim it silently.
        ///
        /// <para>
        /// This is the repair for the case <see cref="GuardOnFunction"/> cannot reach: a Function that arrives
        /// on a node some other way — extraction, a hand edit, a future inspector field — leaves a guard
        /// re-checking every tick, which is legal but the most expensive thing a guard can do.
        /// </para>
        ///
        /// <para>
        /// <b>Only guards with no triggers at all are touched.</b> A guard that already carries a schedule has
        /// had one chosen for it, and an author who deliberately picked an interval — because the condition
        /// depends on something no key can express — must not have that quietly supplemented. Seeding runs
        /// through the same walk the runtime uses, so what is written is what would have been inherited.
        /// </para>
        /// </summary>
        public static List<ConditionalExecution> SeedMissingGuardTriggers(BehaviorTreeGraphAsset asset)
        {
            var seeded = new List<ConditionalExecution>();
            if (asset?.graph == null) return seeded;

            foreach (var node in asset.graph.Nodes)
            {
                // Capability, not type: a guard that carries a schedule without being a ReactiveGuard should
                // be reached by this too, and one that carries none — a doorman — has nothing to schedule.
                if (node is not ConditionalExecution guard || !guard.HasRecomputeSchedule) continue;
                if (guard.Triggers.Count > 0) continue;

                var declared = InheritedWatchedKeys.Resolve(guard);
                if (declared.Length == 0) continue;

                if (guard is ReactiveGuard watchman) watchman.AddTrigger(GuardTrigger.KeyChanged(declared));
                else continue;

                seeded.Add(guard);
            }

            return seeded;
        }
    }
}

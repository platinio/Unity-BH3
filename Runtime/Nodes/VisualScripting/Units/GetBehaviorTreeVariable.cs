using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Reads a variable. The counterpart to <see cref="SetBehaviorTreeVariable"/>.
    ///
    /// <para>
    /// <b>This does nothing Unity's own Get Variable does not</b>, except offer the tree's kinds. Reads are
    /// not recorded and there is nothing here to record — a read changes nothing, and a tree reads
    /// constantly, so an event per read would bury the writes that actually explain a branch change. Do not
    /// expect it to appear in a recording.
    /// </para>
    ///
    /// <para>
    /// The kinds are <see cref="BehaviorTreeVariableKind"/>, the same the tree's own variable nodes offer,
    /// so there is no <c>Flow</c>: flow scratch is not tree state, nothing in a tree can read it, and a unit
    /// named after the tree offering it was offering a store the recorder already had to throw away. Per-flow
    /// scratch is what Unity's own Get and Set Variable are for. Kind <see cref="BehaviorTreeVariableKind.Graph"/>
    /// is the script graph's own variables here, not the tree branch's scope: a script graph has no branch.
    /// </para>
    ///
    /// <para>
    /// It exists so the BH3 variable menu is complete, and that is a practical reason rather than a tidy
    /// one. Set has to be BH3's, because Unity's cannot be observed and a write nobody sees is the one gap
    /// that leaves the why-inspector unable to name a cause. A designer who finds only Set here goes to
    /// Unity's menu for Get, and the next thing under their cursor is Unity's Set — the invisible one. Both
    /// units in one place keeps that from happening.
    /// </para>
    /// </summary>
    // Extends Unit rather than UnifiedVariableUnit for the reason recorded on SetBehaviorTreeVariable: that
    // base carries an inherited [SpecialUnit], which excludes subclasses from the node library entirely.
    // Ports are fully qualified because inside this namespace the bare names are the behavior tree's own.
    [Unity.VisualScripting.UnitCategory("BH3/Variables")]
    [Unity.VisualScripting.UnitTitle("Get Behavior Tree Variable")]
    [Unity.VisualScripting.UnitShortTitle("Get BT Variable")]
    public sealed class GetBehaviorTreeVariable : Unity.VisualScripting.Unit
    {
        /// <summary>
        /// Which variable store this reads from. Starts on the agent's facts, the read a Function most often
        /// makes; a unit that arrives on <see cref="BehaviorTreeVariableKind.None"/> (one saved on the old
        /// <c>Flow</c> kind) is flagged on the canvas and refuses to guess if it runs anyway.
        /// </summary>
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable, Unity.VisualScripting.UnitHeaderInspectable]
        public BehaviorTreeVariableKind kind { get; set; } = BehaviorTreeVariableKind.Object;

        /// <summary>
        /// Whether to return <see cref="fallback"/> when the variable is not declared.
        /// <para>
        /// Worth turning on for anything a reused branch reads. Without it, reading a name the agent does not
        /// declare throws, and a branch that is safe on one prefab throws on another — which is the failure
        /// the fallback exists to turn into a modelled state.
        /// </para>
        /// </summary>
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable, Unity.VisualScripting.InspectorLabel("Fallback")]
        public bool specifyFallback { get; set; }

        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabelHidden]
        public Unity.VisualScripting.ValueInput name { get; private set; }

        /// <summary>The object whose variables are read, when <see cref="kind"/> is Object. Null means self.</summary>
        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabelHidden, Unity.VisualScripting.NullMeansSelf]
        public Unity.VisualScripting.ValueInput @object { get; private set; }

        [Unity.VisualScripting.DoNotSerialize]
        public Unity.VisualScripting.ValueInput fallback { get; private set; }

        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabelHidden]
        public Unity.VisualScripting.ValueOutput value { get; private set; }

        protected override void Definition()
        {
            name = ValueInput(nameof(name), string.Empty);

            if (kind == BehaviorTreeVariableKind.Object)
            {
                @object = ValueInput<GameObject>(nameof(@object), null).NullMeansSelf();
            }

            value = ValueOutput(nameof(value), Get).PredictableIf(IsDefined);

            Requirement(name, value);

            if (kind == BehaviorTreeVariableKind.Object)
            {
                Requirement(@object, value);
            }

            if (specifyFallback)
            {
                fallback = ValueInput<object>(nameof(fallback));
                Requirement(fallback, value);
            }
        }

        private object Get(Unity.VisualScripting.Flow flow)
        {
            var key = flow.GetValue<string>(name);
            var declarations = BehaviorTreeVariableStore.Of(flow, VariableKindField.Resolve(kind, UnitName), @object);

            // Nowhere to read from is the same outcome as not declared: the fallback answers if there is one,
            // and otherwise the read fails the way Visual Scripting's own would.
            if (declarations == null || !declarations.IsDefined(key))
            {
                if (specifyFallback) return flow.GetValue(fallback);

                // No store at all is a different fault from a name that is merely undeclared, and saying
                // which one it is saves the reader from hunting for a variable that was never the problem.
                // A declared-but-missing name falls through so Visual Scripting reports it as it always has.
                if (declarations == null)
                {
                    throw new System.InvalidOperationException(
                        $"Get BT Variable: no {kind} variables to read '{key}' from. " +
                        (kind == BehaviorTreeVariableKind.Object
                            ? "The Object port resolved to nothing."
                            : "The scene is not loaded."));
                }
            }

            return declarations.Get(key);
        }

        /// <summary>
        /// Whether the editor can show a predicted value without running the graph. Kept conservative: an
        /// unloaded scene or a scene with no variables object must read as "cannot predict" rather than
        /// causing one to be created at edit time.
        /// </summary>
        private bool IsDefined(Unity.VisualScripting.Flow flow)
        {
            var key = flow.GetValue<string>(name);
            if (string.IsNullOrEmpty(key) || kind == BehaviorTreeVariableKind.None) return false;

            if (kind == BehaviorTreeVariableKind.Scene)
            {
                if (!BehaviorTreeVariableStore.IsSceneUsable(flow)) return false;
                if (!Unity.VisualScripting.Variables.ExistInScene(flow.stack.scene)) return false;
            }

            var declarations = BehaviorTreeVariableStore.Of(flow, kind, @object);

            return declarations != null && declarations.IsDefined(key);
        }

        /// <summary>What a throw calls this unit; the same words the canvas shows.</summary>
        internal const string UnitName = "Get BT Variable";
    }
}

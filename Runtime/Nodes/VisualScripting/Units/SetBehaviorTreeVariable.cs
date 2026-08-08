using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Visual Scripting's <c>Set Variable</c>, with the write reported to the agent's flight recorder.
    ///
    /// <para>
    /// Unity's own unit cannot be observed from outside: <c>VariableDeclarations.OnVariableChanged</c> is
    /// internal and carries no name, no old value and no new one. So a variable written from inside a script
    /// graph used to be invisible to the recording — and since the why-inspector's central question is "who
    /// changed the value that flipped this guard?", an invisible writer is the one gap that makes the answer
    /// unavailable exactly when it matters. Using this unit instead of the built-in one closes that.
    /// </para>
    ///
    /// <para>
    /// <b>It behaves identically to the built-in unit.</b> Same ports, same kinds, same assignment semantics,
    /// including <see cref="VariableKind.Graph"/> meaning <em>this script graph's</em> variables rather than
    /// the behavior tree branch's scope — a branch scope belongs to a behavior tree node, and a script graph
    /// does not have one. Any divergence in behaviour would be a trap, so the only difference is that this one
    /// is visible in a recording.
    /// </para>
    ///
    /// <para>
    /// The recording costs a shipped build nothing. <see cref="Debugging.BehaviorTreeRecorder"/> is
    /// <c>[Conditional]</c>-gated, so outside the editor and dev builds the compiler removes the call
    /// <em>and its arguments</em> — which is why the machine lookup and the read of the previous value are
    /// written inline as arguments rather than as locals. The assignment itself is outside that call and
    /// always happens.
    /// </para>
    /// </summary>
    // Ports are fully qualified throughout. Inside this namespace the bare names ValueInput, ValueOutput and
    // Literal resolve to the behavior tree's own port types, not Visual Scripting's, and a `using` does not
    // change that — the nearer namespace always wins.
    // Extends Unit directly rather than UnifiedVariableUnit, which would have been the obvious base. That
    // type carries [SpecialUnit], the attribute is declared Inherited, and UnitBase excludes any type
    // carrying it from the node library — so a subclass compiles, runs, and never appears in the fuzzy
    // finder. The three ports it would have supplied are declared below instead.
    [Unity.VisualScripting.UnitCategory("BH3/Variables")]
    [Unity.VisualScripting.UnitTitle("Set Behavior Tree Variable")]
    [Unity.VisualScripting.UnitShortTitle("Set BT Variable")]
    [Unity.VisualScripting.UnitSurtitle("Recorded")]
    public sealed class SetBehaviorTreeVariable : Unity.VisualScripting.Unit
    {
        /// <summary>Which variable store this writes to. Mirrors Visual Scripting's own kind selector.</summary>
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable, Unity.VisualScripting.UnitHeaderInspectable]
        public Unity.VisualScripting.VariableKind kind { get; set; } = Unity.VisualScripting.VariableKind.Object;

        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabelHidden]
        public Unity.VisualScripting.ValueInput name { get; private set; }

        /// <summary>The object whose variables are written, when <see cref="kind"/> is Object. Null means self.</summary>
        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabelHidden, Unity.VisualScripting.NullMeansSelf]
        public Unity.VisualScripting.ValueInput @object { get; private set; }

        /// <summary>What an explanation calls this write when it has no better name.</summary>
        private const string DefaultWriterName = "Script Graph";

        /// <summary>
        /// What the why-inspector should call this writer. A script graph has no node guid to point at, so a
        /// name is all a recording can carry — worth setting to something a designer would recognise when a
        /// tree has several graphs writing the same fact.
        /// </summary>
        [Unity.VisualScripting.Serialize, Unity.VisualScripting.Inspectable]
        public string writerName;

        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabelHidden]
        public Unity.VisualScripting.ControlInput assign { get; private set; }

        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabel("New Value"), Unity.VisualScripting.PortLabelHidden]
        public Unity.VisualScripting.ValueInput input { get; private set; }

        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabelHidden]
        public Unity.VisualScripting.ControlOutput assigned { get; private set; }

        [Unity.VisualScripting.DoNotSerialize, Unity.VisualScripting.PortLabel("Value"), Unity.VisualScripting.PortLabelHidden]
        public Unity.VisualScripting.ValueOutput output { get; private set; }

        protected override void Definition()
        {
            name = ValueInput(nameof(name), string.Empty);

            if (kind == Unity.VisualScripting.VariableKind.Object)
            {
                @object = ValueInput<GameObject>(nameof(@object), null).NullMeansSelf();
            }

            assign = ControlInput(nameof(assign), Assign);
            input = ValueInput<object>(nameof(input)).AllowsNull();
            output = ValueOutput<object>(nameof(output));
            assigned = ControlOutput(nameof(assigned));

            Requirement(name, assign);
            Requirement(input, assign);
            Assignment(assign, output);
            Succession(assign, assigned);

            if (kind == Unity.VisualScripting.VariableKind.Object)
            {
                Requirement(@object, assign);
            }
        }

        private Unity.VisualScripting.ControlOutput Assign(Unity.VisualScripting.Flow flow)
        {
            var key = flow.GetValue<string>(name);
            var value = flow.GetValue(input);

            // Recorded before the write, while the previous value still exists — "what did it change from" is
            // half of what makes a write worth recording. Every argument here is removed by the compiler
            // outside the editor and dev builds, including the lookup and the read.
            Debugging.BehaviorTreeRecorder.ScriptGraphVariableWrite(
                MachineOf(flow), WriterName, key, ReadCurrent(flow, key), value);

            var declarations = Declarations(flow);

            // A write that cannot land has to say so. Silently doing nothing is the worst outcome here: the
            // graph carries on, the value never changes, and the guard reading it looks like the bug.
            if (declarations == null)
            {
                Debug.LogError(
                    $"[BH3] Set BT Variable could not resolve a {kind} store for '{key}'. The write did not happen.",
                    MachineOf(flow));
            }
            else
            {
                declarations.Set(key, value);
            }

            flow.SetValue(output, value);

            return assigned;
        }

        private string WriterName => string.IsNullOrEmpty(writerName) ? DefaultWriterName : writerName;

        /// <summary>
        /// The agent whose recording this belongs to: the object the graph is running on, not the object being
        /// written to. Those differ when a graph writes another agent's variables, and the write is part of
        /// <em>this</em> agent's story either way.
        /// </summary>
        private static BehaviorTreeMachine MachineOf(Unity.VisualScripting.Flow flow)
        {
            var owner = flow?.stack?.gameObject;

            return owner != null ? owner.GetComponent<BehaviorTreeMachine>() : null;
        }

        /// <summary>
        /// Where this write lands. One switch used by both the write and the read of the previous value, so
        /// the two can never disagree about which store they are talking about.
        /// </summary>
        private Unity.VisualScripting.VariableDeclarations Declarations(Unity.VisualScripting.Flow flow)
        {
            switch (kind)
            {
                case Unity.VisualScripting.VariableKind.Flow:
                    return flow.variables;

                case Unity.VisualScripting.VariableKind.Graph:
                    return Unity.VisualScripting.Variables.Graph(flow.stack);

                case Unity.VisualScripting.VariableKind.Object:
                    var target = flow.GetValue<GameObject>(@object);
                    return target != null ? Unity.VisualScripting.Variables.Object(target) : null;

                case Unity.VisualScripting.VariableKind.Scene:
                    return flow.stack.scene.HasValue ? Unity.VisualScripting.Variables.Scene(flow.stack.scene.Value) : null;

                case Unity.VisualScripting.VariableKind.Application:
                    return Unity.VisualScripting.Variables.Application;

                case Unity.VisualScripting.VariableKind.Saved:
                    return Unity.VisualScripting.Variables.Saved;

                default:
                    return null;
            }
        }

        /// <summary>
        /// The value about to be replaced, or null when there is not one yet.
        /// <para>
        /// Never throws. Reading an undefined variable throws in Visual Scripting, and a debugging read has no
        /// business turning a first write into an exception — so an undeclared name reads as null, which is
        /// what a recording should say about a value that did not exist.
        /// </para>
        /// </summary>
        private object ReadCurrent(Unity.VisualScripting.Flow flow, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            var declarations = Declarations(flow);

            return declarations != null && declarations.IsDefined(key) ? declarations.Get(key) : null;
        }
    }
}

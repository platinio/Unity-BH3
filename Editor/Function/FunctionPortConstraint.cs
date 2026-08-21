using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// What a <see cref="VisualScriptGraphVariable"/>'s value is required to be, derived from what its
    /// <c>Output</c> feeds.
    ///
    /// <para>
    /// The node cannot answer this about itself, and knowing why saves rediscovering it:
    /// <c>Definition()</c> declares <c>ValueOutput&lt;object&gt;</c> deliberately, so that one node type can
    /// serve predicates, floats and queries alike. The constraint therefore lives <b>downstream</b> — a
    /// guard's <c>Value</c> is a <see cref="ValueInput"/> of type <c>bool</c>, and that port is the only
    /// thing in the graph that knows <c>bool</c> is required.
    /// </para>
    ///
    /// <para>
    /// A node feeding several ports carries all of their types at once, and a Function has to satisfy every
    /// one of them. A node feeding nothing carries none, which is not an error: authors wire up in whatever
    /// order they like, and a picker that offered nothing until the node was connected would read as broken.
    /// </para>
    /// </summary>
    public readonly struct FunctionPortConstraint
    {
        private static readonly Type[] NoTypes = Array.Empty<Type>();

        private readonly Type[] requiredTypes;

        private FunctionPortConstraint(Type[] requiredTypes)
        {
            this.requiredTypes = requiredTypes;
        }

        /// <summary>
        /// The port types a candidate must satisfy, distinct and in wiring order. Empty when the node feeds
        /// nothing — which <c>default(FunctionPortConstraint)</c> also is, so there is no uninitialised state
        /// separate from "unconstrained".
        /// </summary>
        public IReadOnlyList<Type> RequiredTypes => requiredTypes ?? NoTypes;

        /// <summary>True when nothing downstream constrains the value.</summary>
        public bool IsUnconstrained => RequiredTypes.Count == 0;

        /// <summary>
        /// True when the node feeds several differently-typed ports, so a candidate has to satisfy more than
        /// one thing at once. Worth distinguishing because an empty offer means something different here:
        /// with one required type it means nothing of that type exists yet, with several it can mean the
        /// wiring itself asks for something no value could be.
        /// </summary>
        public bool IsMultiplyConstrained => RequiredTypes.Count > 1;

        /// <summary>
        /// A constraint on types known outright rather than read off wiring -- for a caller that already
        /// knows what it wants, such as a library panel narrowing itself to predicates.
        /// <see cref="For(VisualScriptGraphVariable)"/> is the route when the answer has to be derived from
        /// what the node feeds.
        /// </summary>
        public static FunctionPortConstraint Requiring(params Type[] types)
        {
            if (types == null || types.Length == 0) return default;

            List<Type> distinct = null;

            foreach (var type in types)
            {
                if (type == null) continue;

                distinct ??= new List<Type>();
                if (!distinct.Contains(type)) distinct.Add(type);
            }

            return distinct == null ? default : new FunctionPortConstraint(distinct.ToArray());
        }

        public static FunctionPortConstraint For(VisualScriptGraphVariable node) =>
            node == null ? default : For(node.Output);

        /// <summary>
        /// Reads the constraint off an output's connections.
        ///
        /// <para>
        /// <b>Valid connections only.</b> An invalid connection is one the canvas drew and the type rule
        /// rejected; treating it as a requirement would let already-broken wiring dictate what is offered,
        /// and the repair for that is to fix the wire, which <c>bt_verify</c> already reports.
        /// </para>
        /// </summary>
        public static FunctionPortConstraint For(ValueOutput output)
        {
            if (output == null) return default;

            List<Type> types = null;

            foreach (var destination in output.validConnectedPorts)
            {
                var type = destination?.Type;
                if (type == null) continue;

                types ??= new List<Type>();
                if (!types.Contains(type)) types.Add(type);
            }

            return types == null ? default : new FunctionPortConstraint(types.ToArray());
        }

        /// <summary>
        /// Whether a Function returning <paramref name="resultType"/> can legally fill every port this
        /// constraint covers.
        ///
        /// <para>
        /// <b>Convertibility, not equality, and not merely assignability.</b> The rule is the same one
        /// <see cref="ValueInput.CanConnectToValid"/> uses to decide what may feed a port, and the same one
        /// <see cref="ValueInput.GetValue{T}"/> uses to read it — so what the picker offers and what the
        /// port can actually carry are one decision rather than two that drift. Exact equality would hide a
        /// <c>Transform</c> Function from a <c>Component</c> port, which is the wrongness the evaluation
        /// seam was fixed to stop; assignability alone would additionally hide a <c>float</c> Function from
        /// an <c>int</c> port, which works at runtime.
        /// </para>
        ///
        /// <para>
        /// A <c>null</c> result type is refused even when unconstrained. A Function with no <c>Result</c>
        /// port has no value to hand back, and this node exists to read one — so offering it would offer a
        /// choice that throws on its first evaluation whatever the wiring turns out to be.
        /// </para>
        /// </summary>
        public bool Satisfies(Type resultType)
        {
            if (resultType == null) return false;

            var required = RequiredTypes;

            for (var i = 0; i < required.Count; i++)
            {
                if (!resultType.IsConvertibleTo(required[i], false)) return false;
            }

            return true;
        }

        /// <summary>
        /// The <c>Result</c> type a Function created for this port should declare, or null when the wiring
        /// asks for something no single type can be.
        ///
        /// <para>
        /// This exists because <c>FunctionGraphAsset.DefaultGraph()</c> declares Enter and Exit and no
        /// result — correctly, since it cannot know what the Function is for. A Function created from
        /// <i>this</i> port can know: it is being made to fill the port, so it is born declaring the type
        /// that fills it, and is valid the moment it exists instead of failing the very filter that offered
        /// to create it.
        /// </para>
        ///
        /// <para>
        /// Unconstrained yields <c>object</c> rather than nothing, so an author who wires up afterwards
        /// still gets a Function that returns something.
        /// </para>
        /// </summary>
        public System.Type SuggestedResultType
        {
            get
            {
                var required = RequiredTypes;

                if (required.Count == 0) return typeof(object);
                if (required.Count == 1) return required[0];

                // Several ports: the answer is the most specific required type -- the one every other
                // required type will accept. A Transform result serves both a Transform port and a Component
                // one; a Component result does not reliably serve the Transform port.
                //
                // Note this is strict assignability, NOT the convertibility Satisfies uses, and the
                // difference is deliberate rather than an inconsistency. Satisfies answers "may an author
                // wire this up", and so has to agree with ValueInput.CanConnectToValid, which permits a
                // downcast because the value may well turn out to be the derived type at runtime. This
                // answers "what should a Function created right now declare", where a downcast is a coin
                // flip taken on the author's behalf. Offering leniently and creating conservatively is the
                // right pair; creating leniently would mint a Function that type-checks and then fails on
                // some agents and not others.
                for (var candidate = 0; candidate < required.Count; candidate++)
                {
                    var servesEveryPort = true;

                    for (var port = 0; port < required.Count; port++)
                    {
                        if (required[port].IsAssignableFrom(required[candidate])) continue;

                        servesEveryPort = false;
                        break;
                    }

                    if (servesEveryPort) return required[candidate];
                }

                return null;
            }
        }

        /// <summary>The required types as a readable phrase, for a message rather than for logic.</summary>
        public string Describe()
        {
            var required = RequiredTypes;

            if (required.Count == 0) return "anything";
            if (required.Count == 1) return required[0].Name;

            var names = new string[required.Count];
            for (var i = 0; i < required.Count; i++) names[i] = required[i].Name;

            return string.Join(" and ", names);
        }
    }
}

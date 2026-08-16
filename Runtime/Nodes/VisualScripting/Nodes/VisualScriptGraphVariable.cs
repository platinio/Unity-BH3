using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    [GraphCreateMenu("Unity/Visual Scripting/Script Graph Variable")]
    public class VisualScriptGraphVariable : BaseVisualScriptingNode, IDeclaresWatchedKeys, IFunctionArguments
    {
        [Serialize] [Inspectable] private BTScriptGraphVariable ScriptGraphVariable = null;

        /// <summary>
        /// The Function's contract as this node remembers it, and the only thing <see cref="Definition"/>
        /// reads when declaring ports. See <see cref="VisualScriptingExtension.FunctionParameter"/> for why
        /// it is a copy rather than a live read of the asset: <c>Definition()</c> runs during
        /// deserialization, connections resolve by port key, and a key that does not exist yet is dropped
        /// silently — so declaring ports from the asset loses wiring on any load where it has not resolved,
        /// which is an import-order failure and therefore appears on one machine and not another.
        /// </summary>
        [Serialize]
        private List<VisualScriptingExtension.FunctionParameter> parameters = new();

        /// <summary>
        /// The declared ports, in the same order as <see cref="parameters"/>. An array rather than a
        /// dictionary because it is indexed once per argument per evaluation and never looked up by name.
        /// </summary>
        [DoNotSerialize]
        private ValueInput[] parameterPorts = Array.Empty<ValueInput>();

        /// <summary>Names index-aligned with <see cref="parameterPorts"/>, for resolve-time mapping.</summary>
        [DoNotSerialize]
        private string[] parameterNames = Array.Empty<string>();

        public IReadOnlyList<VisualScriptingExtension.FunctionParameter> Parameters => parameters;
        public override string NodeName => comment == string.Empty? "Script Graph Variable" : comment;
        public override bool CanBeUsedAsTransitionDestination => false;
        public override string Description => "Returns value from Script Graph";

        [DoNotSerialize]
        public ValueOutput Output { get; private set; }

        /// <summary>The embedded graph this node reads, or null when it reads a Function.</summary>
        [DoNotSerialize]
        public ScriptGraphAsset EmbeddedScriptGraph => ScriptGraphVariable?.ScriptGraphAsset;

        /// <summary>The Function this node reads, or null when it reads an embedded graph.</summary>
        [DoNotSerialize]
        public VisualScriptingExtension.FunctionGraphAsset Function => ScriptGraphVariable?.Function;

        /// <summary>
        /// True when both a Function and an embedded graph are assigned. The Function wins at runtime, so
        /// this is not a crash — it is the worse kind of problem, where the graph an author is editing is not
        /// the one being evaluated. Verification reports it.
        /// </summary>
        [DoNotSerialize]
        public bool HasAmbiguousGraphSource =>
            ScriptGraphVariable != null &&
            ScriptGraphVariable.Function != null &&
            ScriptGraphVariable.ScriptGraphAsset != null;

        /// <summary>
        /// The keys the referenced Function declares, so a guard fed by this node inherits its schedule.
        ///
        /// <para>
        /// Read live from the asset rather than copied onto this node, deliberately. A copy would need its own
        /// refresh verb and its own staleness report — a second drift story on top of the one the caller-side
        /// contract copy genuinely requires — and locked decision 5 of spec 10 says there is only ever one.
        /// The copy exists there because <c>Definition()</c> drops connections to ports that do not resolve
        /// during deserialization; nothing about a key list has that problem, so nothing here needs the cure.
        /// </para>
        ///
        /// <para>
        /// An <b>embedded</b> graph declares nothing and yields nothing: it has no asset-level metadata to
        /// declare with. That is the honest answer rather than a walk of its units, which would report derived
        /// keys where every other implementer reports declared ones and quietly make the two mean the same
        /// thing.
        /// </para>
        /// </summary>
        [DoNotSerialize]
        public IReadOnlyList<string> DeclaredWatchedKeys =>
            Function != null ? Function.WatchedKeys : Array.Empty<string>();

        public override bool DrawInSubTree => false;

        /// <summary>
        /// Points this node at the graph that produces its value. Only useful after the node has been added
        /// to a graph, since <see cref="Definition"/> is what creates the variable this assigns into.
        /// Needed to author a node's Visual Scripting from code.
        /// </summary>
        public void SetScriptGraph(ScriptGraphAsset asset)
        {
            ScriptGraphVariable?.SetScriptGraphAsset(asset);
        }

        /// <summary>
        /// Points this node at a Function — a named, shared, contracted graph — instead of an anonymous
        /// sub-asset. The counterpart of <see cref="SetScriptGraph"/> for the new seam.
        /// </summary>
        /// <summary>
        /// Points this node at a Function and grows a port per declared input.
        /// <para>
        /// The refresh is part of assigning rather than a second step an author has to remember: a node
        /// pointed at a Function whose inputs it does not declare has no way to be fed, which is the state
        /// step 2b exists to make unreachable.
        /// </para>
        /// </summary>
        public void SetFunction(VisualScriptingExtension.FunctionGraphAsset asset)
        {
            if (ScriptGraphVariable == null) return;

            ScriptGraphVariable.SetFunction(asset);
            RefreshParameters();
        }
        
        /// <summary>
        /// Declares one port per remembered parameter, plus the output. Reads <see cref="parameters"/> and
        /// nothing else, so it is deterministic from this node's own serialized data and unaffected by
        /// whether the Function asset happens to have resolved yet.
        /// </summary>
        protected override void Definition()
        {
            base.Definition();

            DeclareParameterPorts();

            if (ScriptGraphVariable == null) ScriptGraphVariable = CreateGraphWithOutput(typeof(object));
            Output = ValueOutput<object>(nameof(Output), () =>
            {
                // Announced for the duration of the call so a Set BT Variable unit inside the graph can be
                // attributed to this node rather than to a bare name. Compiles out with the recorder.
                Debugging.BehaviorTreeRecorder.PushScriptGraphOwner(this);

                try
                {
                    runtimeException = null;

                    // A Function is fed by this node's declared ports; an embedded graph still reads the
                    // ambient scope, because it has no contract to declare ports from.
                    return ScriptGraphVariable.ReadsFunction
                        ? ScriptGraphVariable.GetValue<object>(gameObject, this)
                        : ScriptGraphVariable.GetValue<object>(gameObject, ScriptGraphVariables);
                }
                catch (Exception e)
                {
                    LastExecutionStatus = ExecutionStatus.Exception;
                    runtimeException = e;
                    throw;
                }
                finally
                {
                    // A graph that throws must still leave the stack balanced, or every later write in this
                    // agent is attributed to a node that finished long ago.
                    Debugging.BehaviorTreeRecorder.PopScriptGraphOwner();
                }
            });
        }

        public override ExecutionStatus OnUpdate()
        {
            if (runtimeException != null) return ExecutionStatus.Exception;
            return base.OnUpdate();
        }

        // ------------------------------------------------------------------ declared inputs as ports

        private void DeclareParameterPorts()
        {
            if (parameters == null || parameters.Count == 0)
            {
                parameterPorts = Array.Empty<ValueInput>();
                parameterNames = Array.Empty<string>();
                return;
            }

            var ports = new List<ValueInput>(parameters.Count);
            var names = new List<string>(parameters.Count);

            foreach (var parameter in parameters)
            {
                if (parameter == null || string.IsNullOrEmpty(parameter.Name) || parameter.Type == null) continue;
                if (names.Contains(parameter.Name)) continue;

                // Optional declares a default so the port is safe to leave unconnected; required declares
                // none, which makes an unconnected one the unset-port case bt_verify already reports rather
                // than a KeyNotFoundException on the first evaluation.
                ports.Add(parameter.Optional
                    ? ValueInput(parameter.Type, parameter.Name, parameter.DefaultValue)
                    : ValueInput(parameter.Type, parameter.Name));

                names.Add(parameter.Name);
            }

            parameterPorts = ports.ToArray();
            parameterNames = names.ToArray();
        }

        /// <summary>
        /// Rebuilds the remembered contract from the Function's declared inputs and re-declares the ports.
        /// <para>
        /// Returns one line per connection this cost, because a refresh that removes a port removes whatever
        /// fed it, and an author who is not told has no way to notice until the value silently stops
        /// arriving. Reporting is the mitigation; there is deliberately no undo.
        /// </para>
        /// </summary>
        public List<string> RefreshParameters()
        {
            var lost = DescribeConnectionsLostByRefresh();

            parameters = VisualScriptingExtension.FunctionParameter.ReadContract(Function);

            // The staged-argument map is keyed on the plan and the argument count, neither of which changes
            // when a parameter is renamed but the count stays the same.
            ScriptGraphVariable?.InvalidateArgumentMap();

            Define();
            PortsChanged();

            return lost;
        }

        /// <summary>
        /// Which currently-connected ports a refresh would remove, named with what feeds them. Computed
        /// before the rebuild, because afterwards the connection is already gone.
        /// </summary>
        private List<string> DescribeConnectionsLostByRefresh()
        {
            var lost = new List<string>();
            if (parameters == null || parameters.Count == 0) return lost;

            var current = VisualScriptingExtension.FunctionParameter.ReadContract(Function);

            for (var i = 0; i < parameterPorts.Length; i++)
            {
                var name = parameterNames[i];
                if (current.Exists(candidate => candidate.Name == name)) continue;

                var source = ContractPorts.DescribeWhatFeeds(parameterPorts[i]);
                if (source == null) continue;

                lost.Add($"'{NodeName}': removing input '{name}' dropped its connection from {source}.");
            }

            return lost;
        }

        /// <summary>
        /// How the remembered contract differs from what the Function declares now, one line per difference
        /// and empty when they agree. This is the check that makes the copy safe: a renamed or retyped input
        /// shows up here instead of silently unwiring this node.
        /// </summary>
        public List<string> DescribeContractDrift()
        {
            var drift = new List<string>();
            if (Function == null) return drift;

            foreach (var line in VisualScriptingExtension.FunctionParameter.DescribeDrift(Function, parameters))
            {
                drift.Add(line);
            }

            return drift;
        }

        // ------------------------------------------------------------------ IFunctionArguments

        int IFunctionArguments.Count => parameterPorts.Length;

        string IFunctionArguments.NameAt(int index) => parameterNames[index];

        object IFunctionArguments.ValueAt(int index)
        {
            try
            {
                return parameterPorts[index].GetValue();
            }
            catch (MissingValuePortInputException e)
            {
                // A required port with nothing connected, named with this node and the input rather than
                // failing inside the Function with no sign of where the value was owed.
                //
                // Only this exception is relabelled. A connected port evaluates whatever feeds it, which can
                // be another whole graph, so catching everything here would put "the port has nothing
                // connected" on top of a fault several hops away and send whoever is debugging it in exactly
                // the wrong direction.
                throw new Exception(
                    $"'{NodeName}' cannot supply '{parameterNames[index]}' to " +
                    $"{(Function != null ? Function.name : "its Function")}: the port has nothing connected " +
                    "and declares no default.", e);
            }
        }
    }
}
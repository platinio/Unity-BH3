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
        /// The Function's declared <c>Result</c> type as this node remembers it, and what <see cref="Output"/>
        /// is declared as. Null means "no Function, or one with no Result", and the port falls back to
        /// <c>object</c> -- generic, because nothing is known yet.
        ///
        /// <para>
        /// Remembered for the same reason <see cref="parameters"/> is, and it is the half step 2b left out:
        /// that step typed the inputs from the contract copy and left the output hard-coded to <c>object</c>.
        /// An <c>object</c> output converts to nearly anything, so the connection gate could never refuse a
        /// wire -- a Function returning <c>bool</c> could feed a <c>Transform</c> port and the failure arrived
        /// at the first tick. Typing the port from the remembered result is what lets the gate do its job,
        /// and remembering it rather than reading the asset live is what keeps the port's type stable during
        /// deserialization, where an unresolved asset would otherwise retype the port to <c>object</c> and
        /// silently re-accept every connection on one machine and not another.
        /// </para>
        /// </summary>
        [Serialize]
        private Type resultType;

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

        /// <summary>What <see cref="Output"/> is declared as: the remembered result type, or <c>object</c>.</summary>
        [DoNotSerialize]
        public Type OutputType => resultType ?? typeof(object);

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

        /// <summary>
        /// Drawn in a parent tree's sub-tree preview, wires included, because a branch whose guards and
        /// parameters are fed by these nodes reads as unconditional without them.
        /// </summary>
        public override bool DrawInSubTree => true;

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
        public List<string> SetFunction(VisualScriptingExtension.FunctionGraphAsset asset)
        {
            if (ScriptGraphVariable == null) return new List<string>();

            ScriptGraphVariable.SetFunction(asset);
            return RefreshParameters();
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

            // Typed from the remembered result, never from the asset -- see resultType. The getter still
            // hands back object; the declared type is what the connection gate and the canvas read.
            Output = ValueOutput(OutputType, nameof(Output), () =>
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

            // The output retypes here too. Define() re-validates every connection through NodePreservation:
            // one the new type can no longer feed is not removed but demoted to an invalid connection, drawn
            // red on the canvas and reported by bt_verify, so the author sees exactly which wire stopped
            // fitting. It is described before the rebuild for the same reason the lost inputs are.
            var nextResultType = Function?.ResultType;
            lost.AddRange(DescribeConnectionsOutputCanNoLongerFeed(nextResultType ?? typeof(object)));
            resultType = nextResultType;

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
        /// Which currently-fed ports a retyped output will stop fitting, named with what they belong to.
        /// Uses the same rule the connection gate uses, so this says "will become invalid" only when the gate
        /// would now refuse the wire.
        /// </summary>
        private List<string> DescribeConnectionsOutputCanNoLongerFeed(Type nextType)
        {
            var lost = new List<string>();
            if (Output == null || graph == null || nextType == OutputType) return lost;

            foreach (var connection in Output.validConnections)
            {
                var destination = connection.destination;
                if (destination == null || nextType.IsConvertibleTo(destination.Type, false)) continue;

                var owner = destination.behaviorTreeNode is BehaviorTreeNode node ? $"'{node.NodeName}'" : "a node";

                lost.Add(
                    $"'{NodeName}': Output is now {nextType.Name}, which cannot feed {owner}'s '{destination.key}' " +
                    $"({destination.Type.Name}); that connection is invalid until it is rewired.");
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

            // The result is part of the contract too; a Function that changed what it returns has retyped
            // nothing on this node until it is refreshed, and until then the port is lying about its type.
            var declared = Function.ResultType;

            if (declared != resultType)
            {
                drift.Add(
                    $"Result: this node declares its Output as {OutputType.Name}, but the Function now returns " +
                    $"{(declared == null ? "nothing" : declared.Name)}.");
            }

            return drift;
        }

        // ------------------------------------------------------------------ problems

        /// <summary>
        /// What is wrong with this node, for the canvas to draw. Everything here is something the node can
        /// see in itself without walking anything: a contract comparison and two null checks.
        /// </summary>
        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            if (Function == null && EmbeddedScriptGraph == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No Function or graph assigned, so this node has nothing to read."));
                return;
            }

            if (HasAmbiguousGraphSource)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Warning,
                    "Both a Function and an embedded graph are assigned. The Function is what runs.",
                    "Clear whichever one is not wanted — the other is editable but dead."));
            }

            if (Function == null) return;

            foreach (var line in DescribeContractDrift())
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error, line, "Refresh Ports."));
            }

            ReportUndeclaredReads(into);

            // An unfed required port is reported by the base for every node, so it is not repeated here.
        }

        /// <summary>
        /// A key the Function's graph reads but the Function does not declare.
        ///
        /// <para>
        /// <b>This is the one that silently breaks a guard.</b> Inheritance hands a reactive guard the keys a
        /// Function <em>declares</em> — never what a walk of its units finds, deliberately, so that "declares"
        /// and "happens to read" stay different words. The consequence is that adding a Get Variable unit to
        /// a Function does not make any guard reading it wake on that variable. The guard keeps its old
        /// schedule, the fact it now depends on never marks it dirty, and the branch simply stops firing with
        /// nothing anywhere saying why.
        /// </para>
        ///
        /// <para>
        /// Distinct from the trigger drift reported on the guard itself: there the declaration was right and
        /// only the written-down copy lagged, so behaviour was correct. Here the declaration is wrong, so
        /// behaviour is wrong — and refreshing the guard's keys would <em>not</em> help, because it copies
        /// from the declaration that is missing the key. The fix is on the Function.
        /// </para>
        ///
        /// <para>
        /// Reported here, on the node holding the reference, because this is where the Function is visible to
        /// an author. <c>bt_verify</c> has reported it since the foundation pass; a designer does not run it.
        /// </para>
        /// </summary>
        private void ReportUndeclaredReads(List<NodeProblem> into)
        {
            foreach (var key in UndeclaredReadKeys)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Warning,
                    $"{Function.name} reads '{key}' but does not declare it as a watched key, so a reactive "
                    + "guard reading this Function will never wake on it.",
                    "Declare it on the Function: fn_set_metadata --watched_keys."));
            }
        }

        /// <summary>
        /// Keys the Function's graph reads that its declaration omits. Derived rather than declared, and so
        /// only ever used to report — never to schedule anything, which is the distinction
        /// <see cref="DeclaredWatchedKeys"/> exists to keep.
        /// </summary>
        [DoNotSerialize]
        public IReadOnlyList<string> UndeclaredReadKeys
        {
            get
            {
                if (Function == null || Function.graph == null) return Array.Empty<string>();

                var read = Function.DeriveReadKeys();
                if (read == null || read.Count == 0) return Array.Empty<string>();

                var declared = Function.WatchedKeys;
                List<string> undeclared = null;

                foreach (var key in read)
                {
                    if (string.IsNullOrWhiteSpace(key) || IsDeclared(declared, key)) continue;

                    undeclared ??= new List<string>();
                    undeclared.Add(key);
                }

                return (IReadOnlyList<string>)undeclared ?? Array.Empty<string>();
            }
        }

        /// <summary>
        /// The Function, which is where this node's watched keys are declared and therefore where an
        /// incomplete declaration is repaired. Null for an embedded graph, which declares nothing anywhere.
        /// </summary>
        [DoNotSerialize]
        public UnityEngine.Object DeclarationOwner => Function;

        /// <summary>Membership without LINQ, since this runs on the canvas path.</summary>
        private static bool IsDeclared(IReadOnlyList<string> declared, string key)
        {
            for (var i = 0; i < declared.Count; i++)
            {
                if (declared[i] == key) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ IFunctionArguments

        string IFunctionArguments.CallSiteName => NodeName;

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
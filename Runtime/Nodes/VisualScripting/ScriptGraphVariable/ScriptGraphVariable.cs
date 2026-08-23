using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [System.Serializable]
    public class ScriptGraphVariable
    {
        /// <summary>
        /// The Function this variable reads. The only way it gets a graph.
        /// <para>
        /// Evaluation goes through <see cref="FunctionEvaluator"/>: ports resolved once, a graph reference
        /// cached per agent, no reflection and no scans in the call path. This used to be one of two routes,
        /// the other being a graph embedded in the owning tree as an anonymous sub-asset; that route is gone,
        /// and with it the ambiguity of a variable that had both.
        /// </para>
        /// </summary>
        [SerializeField] protected FunctionGraphAsset function;

        [System.NonSerialized] private FunctionBinding binding;
        [System.NonSerialized] private GameObject boundAgent;

        /// <summary>
        /// The plan the argument map below was resolved against, held to detect a rebuild by reference.
        /// </summary>
        [System.NonSerialized] private FunctionBindingPlan mappedPlan;

        /// <summary>
        /// Argument slot -> index of the declared input it feeds, or -1 for an argument the Function does
        /// not declare. Resolved once and reused, because the alternative is a name lookup per argument per
        /// evaluation on the one path the whole binding plan exists to keep free of them.
        /// </summary>
        [System.NonSerialized] private int[] argumentIndices;

        public FunctionGraphAsset Function => function;

        /// <summary>True when a Function is assigned. Kept as the name every caller already uses.</summary>
        public bool ReadsFunction => function != null;

        /// <summary>
        /// Whether there is anything here to run at all.
        ///
        /// <para>
        /// The same question as <see cref="ReadsFunction"/> now that a Function is the only route, and kept
        /// separate because it is the one callers should ask. While there were two routes,
        /// <c>VisualScriptingNode</c>'s four lifecycle hooks gated on the embedded field specifically, so a
        /// slot holding a Function was skipped entirely and did nothing, silently. Asking "is anything
        /// assigned" rather than "is this particular route assigned" is what made that unrepresentable.
        /// </para>
        /// </summary>
        public bool HasGraph => function != null;

        /// <summary>
        /// Points this variable at a Function.
        /// </summary>
        public void SetFunction(FunctionGraphAsset asset)
        {
            function = asset;
            binding = null;
            boundAgent = null;
            InvalidateArgumentMap();
        }

        /// <summary>
        /// Forces the argument map to be resolved again on the next evaluation.
        /// <para>
        /// Needed because the map is keyed on the binding plan and the argument <em>count</em>, neither of
        /// which changes when a caller renames a parameter while keeping the same number of them. Every path
        /// that can do that is editor-time — refreshing a node's contract, or pointing it at a different
        /// Function — so this is called from there rather than checked per evaluation. Nothing in a player
        /// build can rename a port.
        /// </para>
        /// </summary>
        public void InvalidateArgumentMap()
        {
            mappedPlan = null;
            argumentIndices = null;
        }

        /// <summary>
        /// One binding per agent, rebuilt only when the agent changes. The node instance this lives on is
        /// already per-agent, so the binding's lifetime is the node's and nothing has to reap it.
        /// </summary>
        private FunctionBinding BindingFor(GameObject agent)
        {
            if (binding == null || boundAgent != agent)
            {
                binding = FunctionEvaluator.Bind(function, agent);
                boundAgent = agent;
            }

            return binding;
        }

        /// <summary>
        /// Stages the call site's arguments onto the binding.
        ///
        /// <para>
        /// Arguments arrive from the calling node's own declared ports. They used to be matched by name
        /// against the agent's flattened variable scope, which meant a Function's contract was satisfied by
        /// coincidence — an agent that happened to declare a variable of the same name fed it, and nothing
        /// about that was visible on the node. Ports replaced that outright (spec 10, step 2b): a contract
        /// nobody can see is not a contract.
        /// </para>
        ///
        /// <para>
        /// <b>Names are resolved to indices once, not per evaluation.</b> The obvious implementation — stage
        /// argument <c>i</c> into input <c>i</c> — is wrong, and silently so:
        /// <see cref="FunctionBindingPlan.Resolve"/> skips any declared input that has no live port on the
        /// graph's input unit, while the caller's contract copy lists every declared input. So the two lists
        /// can differ in both length and order, and a positional stage would feed the wrong argument to the
        /// wrong parameter with no error anywhere. The map below is what makes position safe, and it is
        /// rebuilt only when the plan is (by reference) or the argument count changes.
        /// </para>
        /// </summary>
        private void StageArguments(FunctionBinding agentBinding, IFunctionArguments arguments)
        {
            var plan = agentBinding.Plan;

            // Deliberately not an early return on a call site with no arguments: a node with no ports feeding
            // a Function that declares inputs is precisely the drift the coverage check below exists to catch.
            var count = arguments?.Count ?? 0;

            if (argumentIndices == null || argumentIndices.Length != count || !ReferenceEquals(mappedPlan, plan))
            {
                argumentIndices = new int[count];

                for (var i = 0; i < count; i++)
                {
                    argumentIndices[i] = plan.IndexOfInput(arguments.NameAt(i));
                }

                ThrowIfAnInputHasNoArgument(plan, arguments, count);

                mappedPlan = plan;
            }

            if (count == 0) return;

            for (var i = 0; i < count; i++)
            {
                var index = argumentIndices[i];

                // A port for an input the Function no longer declares. Left unstaged rather than fataled:
                // this is exactly the drift bt_verify reports and RefreshParameters repairs, and refusing to
                // run would turn a reported, repairable staleness into a broken agent.
                if (index < 0) continue;

                if (!agentBinding.TrySetArgument(index, arguments.ValueAt(i), out var argumentError))
                {
                    throw new System.InvalidOperationException(argumentError);
                }
            }
        }

        /// <summary>
        /// Refuses to evaluate when the Function declares an input nothing is going to stage.
        ///
        /// <para>
        /// This is the drift direction that actually breaks something. The other way round — a call site
        /// holding a port for an input the Function has dropped — is harmless, because nothing inside the
        /// graph reads it any more. But an input with <em>no</em> argument is never assigned a value at all,
        /// and Visual Scripting reads that as a missing key: a bare
        /// <c>KeyNotFoundException</c> thrown from inside the graph, naming the key and nothing else. That
        /// message sends whoever hits it into the Function, when the thing that needs fixing is the call
        /// site's stale contract copy.
        /// </para>
        ///
        /// <para>
        /// Checked here, where the argument map is resolved, rather than per evaluation — so it costs
        /// nothing in the call path, and a Function that binds once keeps binding for free.
        /// </para>
        /// </summary>
        private void ThrowIfAnInputHasNoArgument(FunctionBindingPlan plan, IFunctionArguments arguments, int count)
        {
            for (var input = 0; input < plan.InputKeys.Length; input++)
            {
                var staged = false;

                for (var i = 0; i < count; i++)
                {
                    if (argumentIndices[i] != input) continue;

                    staged = true;
                    break;
                }

                if (staged) continue;

                var callSite = arguments == null ? "the caller" : $"'{arguments.CallSiteName}'";

                throw new System.InvalidOperationException(
                    $"Function '{function.name}' declares input '{plan.InputKeys[input]}', but {callSite} has "
                    + "no port for it, so nothing supplies it. The call site's copy of the contract is stale: "
                    + "refresh its ports with fn_refresh_ports, which bt_verify also reports as drift.");
            }
        }

        private T EvaluateFunction<T>(GameObject agent, IFunctionArguments arguments)
        {
            var agentBinding = BindingFor(agent);

            if (!agentBinding.TryPrepare(out var prepareError))
            {
                throw new System.InvalidOperationException(prepareError);
            }

            StageArguments(agentBinding, arguments);

            if (!agentBinding.TryEvaluate<T>(out var result, out var error))
            {
                throw new System.InvalidOperationException(error);
            }

            return result;
        }

        private void RunFunction(GameObject agent, IFunctionArguments arguments)
        {
            var agentBinding = BindingFor(agent);

            if (!agentBinding.TryPrepare(out var prepareError))
            {
                throw new System.InvalidOperationException(prepareError);
            }

            StageArguments(agentBinding, arguments);

            if (!agentBinding.TryRun(out var error))
            {
                throw new System.InvalidOperationException(error);
            }
        }

        /// <summary>
        /// Evaluates the Function this variable reads, with the arguments the call site supplies.
        /// <para>
        /// This is the entry point for a node that declares ports from the Function's contract. The
        /// overloads taking <c>Variables</c> or <c>VariableDeclarations</c> reach the same evaluation with
        /// <em>no</em> arguments — they predate ports and describe the ambient scope, which is no longer how
        /// a Function is fed.
        /// </para>
        /// </summary>
        public T GetValue<T>(GameObject gameObject, IFunctionArguments arguments)
        {
            if (!ReadsFunction)
            {
                throw new System.InvalidOperationException(
                    "Arguments were supplied but this Script Graph Variable has no Function assigned, so " +
                    "there is no declared contract to bind them to.");
            }

            return EvaluateFunction<T>(gameObject, arguments);
        }

        /// <summary>Runs the Function for its control flow, with the call site's arguments.</summary>
        public void Run(GameObject gameObject, IFunctionArguments arguments)
        {
            if (!ReadsFunction)
            {
                throw new System.InvalidOperationException(
                    "Arguments were supplied but this Script Graph Variable has no Function assigned, so " +
                    "there is no declared contract to bind them to.");
            }

            RunFunction(gameObject, arguments);
        }

        /// <summary>
        /// Evaluates the Function with no arguments, for a caller that has none to give.
        ///
        /// <para>
        /// There used to be three of these, each taking an ambient variable scope — a <c>Variables</c>
        /// component or a <c>VariableDeclarations</c> — because the embedded route fed a graph by copying
        /// that scope onto its input ports. A Function is fed by declared arguments instead, so when the
        /// embedded route went those parameters stopped doing anything while callers kept passing real
        /// scopes to them. A parameter that is read by nobody and supplied by everybody is worse than no
        /// parameter: it tells the next reader the scope reaches the graph, and their first hour goes on
        /// finding out it does not.
        /// </para>
        /// </summary>
        public T GetValue<T>(GameObject agent = null) => EvaluateFunction<T>(agent, null);

        /// <summary>Runs the Function for its control flow, with no arguments. See <see cref="GetValue{T}"/>.</summary>
        public void Run(GameObject agent = null) => RunFunction(agent, null);

    }
}


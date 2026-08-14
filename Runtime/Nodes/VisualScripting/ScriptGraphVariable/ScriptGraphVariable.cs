using System.Collections.Generic;
using ArcaneOnyx.VisualScriptingExtension;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [System.Serializable]
    public class ScriptGraphVariable
    {
        [SerializeField] protected ScriptGraphAsset scriptGraphAsset;

        /// <summary>
        /// The Function this variable reads, when it reads one.
        /// <para>
        /// Assigning a Function routes evaluation through <see cref="FunctionEvaluator"/>: ports resolved once,
        /// a graph reference cached per agent, no reflection and no scans in the call path. Leaving it null
        /// keeps the legacy <see cref="ScriptGraphAsset"/> path exactly as it was, which is what lets every
        /// existing tree and sample load and behave identically while the two seams coexist.
        /// </para>
        /// </summary>
        [SerializeField] protected FunctionGraphAsset function;

        [System.NonSerialized] private FunctionBinding binding;
        [System.NonSerialized] private GameObject boundAgent;

        public ScriptGraphAsset ScriptGraphAsset => scriptGraphAsset;

        public FunctionGraphAsset Function => function;

        /// <summary>True when this reads a Function rather than a bare script graph.</summary>
        public bool ReadsFunction => function != null;

        /// <summary>
        /// Points this variable at a Function. Mutually exclusive with <see cref="SetScriptGraphAsset"/> in
        /// practice — the Function wins when both are set, and verification reports the ambiguity.
        /// </summary>
        public void SetFunction(FunctionGraphAsset asset)
        {
            function = asset;
            binding = null;
            boundAgent = null;
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
        /// Stages whichever of the Function's declared inputs the caller has a variable for.
        /// <para>
        /// This walks the <em>Function's</em> inputs and pulls each by key, rather than iterating the caller's
        /// declarations and matching names. Two reasons, both measured rather than assumed:
        /// <c>VariableDeclarations.GetEnumerator()</c> is declared to return the interface
        /// <c>IEnumerator&lt;VariableDeclaration&gt;</c>, so a <c>foreach</c> boxes its struct enumerator and
        /// allocates on every single evaluation; and the loop index here <em>is</em> the input index, so no
        /// port is looked up by name in the call path — the rule the whole binding plan exists to enforce.
        /// </para>
        /// <para>
        /// The loop is also over the Function's inputs, typically none or a few, rather than over the agent's
        /// entire declaration set.
        /// </para>
        /// </summary>
        private static void StageArguments(FunctionBinding agentBinding, VariableDeclarations arguments)
        {
            if (arguments == null) return;

            var keys = agentBinding.Plan.InputKeys;

            for (var i = 0; i < keys.Length; i++)
            {
                var key = keys[i];

                // An input the caller has no variable for is left to the Function's own default. These are
                // ambient variables, not a call site's argument list, so gaps are expected.
                if (!arguments.IsDefined(key)) continue;

                if (!agentBinding.TrySetArgument(i, arguments.Get(key), out var argumentError))
                {
                    throw new System.InvalidOperationException(argumentError);
                }
            }
        }

        private T EvaluateFunction<T>(GameObject agent, VariableDeclarations arguments)
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

        private void RunFunction(GameObject agent, VariableDeclarations arguments)
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
        /// Assigns the graph this variable reads from. Needed to build a node's Visual Scripting graph from
        /// code, where the inspector is not involved — the counterpart of
        /// <see cref="RunBehaviorTreeGraphNode.SetBehaviorTreeGraphAsset"/> for sub-trees.
        /// </summary>
        public void SetScriptGraphAsset(ScriptGraphAsset asset)
        {
            scriptGraphAsset = asset;
        }

        public T GetValue<T>(Variables input = null)
        {
            if (ReadsFunction) return EvaluateFunction<T>(null, input == null ? null : input.declarations);

            return scriptGraphAsset.GetScriptGraphOutput<T>(input);
        }

        public T GetValue<T>(GameObject gameObject, Variables input = null)
        {
            if (ReadsFunction) return EvaluateFunction<T>(gameObject, input == null ? null : input.declarations);

            return scriptGraphAsset.GetScriptGraphOutput<T>(input, gameObject);
        }

        public T GetValue<T>(GameObject gameObject, VariableDeclarations variableDeclarations)
        {
            if (ReadsFunction) return EvaluateFunction<T>(gameObject, variableDeclarations);

            Dictionary<string, object> dynamicParameters = new();

            foreach (var variableDeclaration in variableDeclarations)
            {
                dynamicParameters[variableDeclaration.name] = variableDeclaration.value;
            }

            return scriptGraphAsset.GetScriptGraphOutput<T>(dynamicParameters, gameObject);
        }

        public void Run(Variables input = null)
        {
            if (ReadsFunction)
            {
                RunFunction(null, input == null ? null : input.declarations);
                return;
            }

            scriptGraphAsset.Run(input);
        }

        public void Run(GameObject gameObject, Variables input = null)
        {
            if (ReadsFunction)
            {
                RunFunction(gameObject, input == null ? null : input.declarations);
                return;
            }

            scriptGraphAsset.Run(input, gameObject);
        }

        public void Run(GameObject gameObject, VariableDeclarations input = null)
        {
            if (ReadsFunction)
            {
                RunFunction(gameObject, input);
                return;
            }

            scriptGraphAsset.Run(input, gameObject);
        }

    }
}


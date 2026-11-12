#if MODULE_TACTICAL_POSITION_SELECTION_EXIST
using System;
using System.Collections.Generic;
using ArcaneOnyx.UnityTacticalPositionSelection;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>What both TPS nodes report about a missing query or agent, on the canvas and at run time.</summary>
    internal static class TPSQueryNodeChecks
    {
        /// <summary>
        /// What an unconnected Query port means, given the preset the node saved before the port took query sources.
        /// </summary>
        public static void CollectQueryProblems(TacticalPositionSelectionQueryItem savedPreset, List<NodeProblem> into)
        {
            if (savedPreset == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No query connected, so this node has nothing to run.",
                    "Connect a TPS Query literal, or another query source, to the Query port."));
                return;
            }

            into.Add(new NodeProblem(NodeProblemSeverity.Warning,
                $"Runs preset '{savedPreset.Name}', saved before the Query port took query sources; the canvas doesn't show it.",
                "Connect a TPS Query literal holding it, or another query, to the Query port."));

            if (savedPreset.Function == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    $"Query '{savedPreset.Name}' has no Function assigned, so it cannot build a query.",
                    "Assign a Function on the query item."));
            }
        }

        public static InvalidOperationException NoEvaluator(string nodeName, GameObject agentObject)
            => new($"'{nodeName}' has no evaluator: nothing feeds the Evaluator port and " +
                   $"'{(agentObject != null ? agentObject.name : "(no object)")}' has no GameEntity to fall back to.");

        public static InvalidOperationException NoQuery(string nodeName)
            => new($"'{nodeName}' has no query: nothing feeds the Query port.");
    }
}
#endif

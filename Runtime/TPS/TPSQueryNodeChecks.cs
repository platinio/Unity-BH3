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
        public static void CollectQueryProblems(TacticalPositionSelectionQueryItem item, List<NodeProblem> into)
        {
            if (item == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No query connected, so this node has nothing to run.",
                    "Connect a TPS Query literal, or another query source, to the Query port."));
                return;
            }

            if (item.Function == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    $"Query '{item.Name}' has no Function assigned, so it cannot build a query.",
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

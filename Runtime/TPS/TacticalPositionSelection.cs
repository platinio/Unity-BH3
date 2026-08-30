using System.Collections.Generic;
using ArcaneOnyx.AIEntities;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.UnityTacticalPositionSelection;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Feeds a tactical position into the tree: runs the selected query preset through
    /// <c>TrySelectPosition</c> and offers the winning position plus whether it cleared the query's
    /// acceptable score.
    ///
    /// <para>
    /// <b>A data node, not an action.</b> It never enters the execution flow
    /// (<see cref="CanBeUsedAsTransitionDestination"/> is false) and runs nothing on its own schedule; the
    /// query runs when another node pulls one of the outputs. Both outputs describe the <em>same</em> run
    /// — one selection per frame, cached — because a pick mode like RandomAmongBest chooses differently on
    /// every run, and a validity flag from one run beside a position from another is a lie with two ports.
    /// </para>
    ///
    /// <para>
    /// Replaces the retired adapter node that lived in the TPS module. The direction is deliberate: TPS
    /// must not depend on BH3, so the node lives here, in an assembly that exists only when the TPS module
    /// does.
    /// </para>
    /// </summary>
    [GraphCreateMenu("Unity/Navigation/Tactical Position Selection")]
    public class TacticalPositionSelection : GameplayNode
    {
        [Serialize] [Inspectable] private TacticalPositionSelectionQueryItem query;

        [DoNotSerialize]
        public ValueInput TargetPosition { get; private set; }

        [DoNotSerialize]
        public ValueInput Evaluator { get; private set; }

        [DoNotSerialize]
        public ValueOutput SelectedPosition { get; private set; }

        [DoNotSerialize]
        public ValueOutput HasPosition { get; private set; }

        /// <summary>The run both outputs describe. Valid only for <see cref="lastFrame"/>.</summary>
        [DoNotSerialize] private TPSQueryResult lastResult = TPSQueryResult.None;

        [DoNotSerialize] private bool lastValid;

        [DoNotSerialize] private int lastFrame = -1;

        /// <summary>The query preset this node runs, or null when none is selected yet.</summary>
        [DoNotSerialize]
        public TacticalPositionSelectionQueryItem Query => query;

        public override string NodeName => "Tactical Position Selection";
        public override string Description => "Runs the selected tactical position query and feeds the winning position to other nodes";
        public override bool CanBeUsedAsTransitionDestination => false;

        // Wide enough for TargetPosition and SelectedPosition to sit on one row without clipping —
        // ContractPortLayout.ResizeToFitPorts measures these ports at 288. StartingSize is the right tool
        // here, unlike on contract-driven nodes: these four ports are fixed, so the size chosen at
        // creation never goes stale.
        public override Vector2 StartingSize => new(240.0f, 110.0f);

        private const string AIDebugModeTogglePrefKey = "AIDebugModeEnabled";
      

        protected override void Definition()
        {
            base.Definition();

            // Both inputs have a meaning when unconnected — see ResolveEvaluator and Select — so neither
            // may be reported as a missing connection.
            TargetPosition = ValueInput<Vector3>(nameof(TargetPosition)).SafeToLeaveUnconnected();
            Evaluator = ValueInput<ITacticalAgent>(nameof(Evaluator)).SafeToLeaveUnconnected();

            SelectedPosition = ValueOutput<Vector3>(nameof(SelectedPosition), () => Select().Position);
            HasPosition = ValueOutput<bool>(nameof(HasPosition), () =>
            {
                Select();
                return lastValid;
            });
        }

        /// <summary>
        /// The frame's selection, run on the first pull and reread by every later one. The cache is written
        /// only after a run completes, so a pull that throws leaves nothing stale behind and the next pull
        /// fails just as loudly.
        /// </summary>
        private TPSQueryResult Select()
        {
            if (lastFrame == Time.frameCount) return lastResult;

            var evaluator = ResolveEvaluator();

            if (evaluator == null)
            {
                throw new System.InvalidOperationException(
                    $"'{NodeName}' has no evaluator: nothing feeds the Evaluator port and " +
                    $"'{gameObject?.name}' has no GameEntity to fall back to.");
            }

            if (query == null)
            {
                throw new System.InvalidOperationException(
                    $"'{NodeName}' has no query selected. Pick one in this node's inspector.");
            }

            // CreateTacticalPositionSelectionQuery already throws, naming the item, when no Function is
            // assigned or the Function fails the query contract.
            using var selection = query.CreateTacticalPositionSelectionQuery(evaluator);

            var debug = PlayerPrefs.GetInt(AIDebugModeTogglePrefKey, 0) == 1;

            // An unconnected target runs the targetless overload, which centres target-relative knobs on
            // the querier instead of on a zero nobody chose.
            var valid = TargetPosition.hasValidConnection
                ? selection.TrySelectPosition(evaluator, TargetPosition.GetValue<Vector3>(), out var result, debug)
                : selection.TrySelectPosition(evaluator, out result, debug);

            lastValid = valid;
            lastResult = valid ? result : TPSQueryResult.None;
            lastFrame = Time.frameCount;

            return lastResult;
        }

        private ITacticalAgent ResolveEvaluator()
        {
            if (Evaluator.hasValidConnection) return Evaluator.GetValue<ITacticalAgent>();

            return TPSAdapters.GetAgent(gameObject != null ? gameObject.GetComponent<GameEntity>() : null);
        }

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            if (query == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No query selected, so this node has nothing to run.",
                    "Pick a query in this node's inspector."));
                return;
            }

            if (query.GeneratorFunction == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    $"Query '{query.Name}' has no Function assigned, so it cannot build a query.",
                    "Assign a Function on the query item."));
            }
        }
    }
}

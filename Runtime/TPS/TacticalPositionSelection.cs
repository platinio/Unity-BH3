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
    /// <c>TrySelectPosition</c> and offers the winning position plus whether it cleared this node's
    /// <see cref="MinimumScore"/> — the act/no-act bar is authored where the acting happens, not on the
    /// query asset.
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
        // The pre-port serialized slot, kept (without [Inspectable]) so trees authored before Query was a
        // port keep their selection: ResolveQuery falls back to it whenever the port yields nothing. The
        // one edge that buys: a port cleared back to None on such a tree resolves to the legacy value —
        // pick a query and save once to leave the legacy slot behind for good.
        [Serialize] private TacticalPositionSelectionQueryItem query;

        [DoNotSerialize]
        public ValueInput Query { get; private set; }

        [DoNotSerialize]
        public ValueInput TargetPosition { get; private set; }

        [DoNotSerialize]
        public ValueInput Evaluator { get; private set; }

        [DoNotSerialize]
        public ValueInput MinimumScore { get; private set; }

        [DoNotSerialize]
        public ValueOutput SelectedPosition { get; private set; }

        [DoNotSerialize]
        public ValueOutput HasPosition { get; private set; }

        /// <summary>The run both outputs describe. Valid only for <see cref="lastFrame"/>.</summary>
        [DoNotSerialize] private TPSQueryResult lastResult = TPSQueryResult.None;

        [DoNotSerialize] private bool lastValid;

        [DoNotSerialize] private int lastFrame = -1;

        public override string NodeName => "Tactical Position Selection";
        public override string Description => "Runs the selected tactical position query and feeds the winning position to other nodes";
        public override bool CanBeUsedAsTransitionDestination => false;

        // Wide enough for TargetPosition and SelectedPosition to sit on one row without clipping —
        // ContractPortLayout.ResizeToFitPorts measures these ports at 288. StartingSize is the right tool
        // here, unlike on contract-driven nodes: these six ports are fixed, so the size chosen at
        // creation never goes stale.
        public override Vector2 StartingSize => new(240.0f, 170.0f);

        private const string AIDebugModeTogglePrefKey = "AIDebugModeEnabled";
      

        protected override void Definition()
        {
            base.Definition();

            // A port rather than an inspector field, so a query can arrive the way any other argument
            // does — a graph, a variable, a sub-tree parameter — while the inline default keeps the
            // dropdown workflow (the item type's registered Inspector draws it). Declared with the legacy
            // field as its default so pre-port trees migrate on load without an asset touch.
            Query = ValueInput<TacticalPositionSelectionQueryItem>(nameof(Query), query);

            // Both inputs have a meaning when unconnected — see ResolveEvaluator and Select — so neither
            // may be reported as a missing connection.
            TargetPosition = ValueInput<Vector3>(nameof(TargetPosition)).SafeToLeaveUnconnected();
            Evaluator = ValueInput<ITacticalAgent>(nameof(Evaluator)).SafeToLeaveUnconnected();

            // The act/no-act bar is the caller's, so it lives here on the tree rather than on the query
            // asset: 0 accepts any non-vetoed winner, and HasPosition reports what the bar rejected.
            MinimumScore = ValueInput<float>(nameof(MinimumScore), 0f);

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

            var item = ResolveQuery();

            if (item == null)
            {
                throw new System.InvalidOperationException(
                    $"'{NodeName}' has no query: nothing feeds the Query port and no inline value is set.");
            }

            // CreateTacticalPositionSelectionQuery already throws, naming the item, when no Function is
            // assigned or the Function fails the query contract.
            using var selection = item.CreateTacticalPositionSelectionQuery(evaluator);

            var debug = PlayerPrefs.GetInt(AIDebugModeTogglePrefKey, 0) == 1;

            var minimumScore = MinimumScore.GetValue<float>();

            // An unconnected target runs the targetless overload, which centres target-relative knobs on
            // the querier instead of on a zero nobody chose.
            var valid = TargetPosition.hasValidConnection
                ? selection.TrySelectPosition(
                    evaluator, TargetPosition.GetValue<Vector3>(), out var result, minimumScore, debug)
                : selection.TrySelectPosition(evaluator, out result, minimumScore, debug);

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

        /// <summary>The port's answer — connection or inline value — falling back to the legacy field.</summary>
        private TacticalPositionSelectionQueryItem ResolveQuery()
        {
            var item = Query.GetValue<TacticalPositionSelectionQueryItem>();

            return item != null ? item : query;
        }

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            // A connected port is a runtime answer; there is nothing to check statically without pulling
            // the connection, which a problem scan must not do.
            if (Query.hasValidConnection) return;

            var item = ResolveQuery();

            if (item == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "No query selected, so this node has nothing to run.",
                    "Pick a query on the Query port, or feed the port a connection."));
                return;
            }

            if (item.GeneratorFunction == null)
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    $"Query '{item.Name}' has no Function assigned, so it cannot build a query.",
                    "Assign a Function on the query item."));
            }
        }
    }
}

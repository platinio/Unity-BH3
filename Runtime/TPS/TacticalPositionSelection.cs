#if MODULE_TACTICAL_POSITION_SELECTION_EXIST
using System.Collections.Generic;
using ArcaneOnyx.AIEntities;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.Scoring;
using ArcaneOnyx.UnityTacticalPositionSelection;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Feeds a tactical position into the tree: runs the query source on the Query port and offers the winning
    /// position plus whether it cleared this node's <see cref="MinimumScore"/> — the act/no-act bar is authored
    /// where the acting happens, not on the query asset. The query is built once per agent and run on every pull.
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
    [GraphCreateMenu("Navigation/Tactical Position Selection")]
    public class TacticalPositionSelection : GameplayNode
    {
        private const int NoFrame = -1;

        // A preset saved before the port took query sources: picked here before Query was a port, or inline on the
        // port while it took a query item. ResolveSource falls back to it whenever nothing is connected.
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
        public ValueInput DrawGizmo { get; private set; }

        [DoNotSerialize]
        public ValueOutput SelectedPosition { get; private set; }

        [DoNotSerialize]
        public ValueOutput HasPosition { get; private set; }

        public override string NodeName => "Tactical Position Selection";
        public override string Description => "Runs the selected tactical position query and feeds the winning position to other nodes";
        public override bool CanBeUsedAsTransitionDestination => false;

        // Wide enough for TargetPosition and SelectedPosition to sit on one row without clipping —
        // ContractPortLayout.ResizeToFitPorts measures these ports at 288. StartingSize is the right tool
        // here, unlike on contract-driven nodes: these seven ports are fixed, so the size chosen at
        // creation never goes stale.
        public override Vector2 StartingSize => new(240.0f, 200.0f);

        /// <summary>The instance this node built for its agent and keeps between pulls; null before the first.</summary>
        internal IPositionQueryInstance BuiltInstance => Context.Memory<Memory>().Built.Current;

        private sealed class Memory
        {
            public readonly BuiltPositionQuery Built = new();

            /// <summary>The run both outputs describe. Valid only for <see cref="LastFrame"/>.</summary>
            public TPSQueryResult LastResult = TPSQueryResult.None;

            public int LastFrame = NoFrame;
        }

        public override void AfterAdd()
        {
            KeepThePresetPickedOnThePort();
            base.AfterAdd();
        }

        /// <summary>Drops this frame's cached selection, so the next pull runs the query again.</summary>
        internal void ForgetThisFramesSelection() => Context.Memory<Memory>().LastFrame = NoFrame;

        protected override void Definition()
        {
            base.Definition();

            // A port rather than an inspector field, so a query can arrive the way any other argument does — a
            // TPS Query literal, a variable, a sub-tree parameter. An interface port has no inline value, so an
            // unconnected port is reported by CollectProblems, not as a missing connection.
            Query = ValueInput<IPositionQuerySource>(nameof(Query)).SafeToLeaveUnconnected();

            // Both inputs have a meaning when unconnected — see ResolveEvaluator and Select — so neither
            // may be reported as a missing connection.
            TargetPosition = ValueInput<Vector3>(nameof(TargetPosition)).SafeToLeaveUnconnected();
            Evaluator = ValueInput<ITacticalAgent>(nameof(Evaluator)).SafeToLeaveUnconnected();

            // The act/no-act bar is the caller's, so it lives here on the tree rather than on the query
            // asset: 0 accepts any non-vetoed winner, and HasPosition reports what the bar rejected.
            MinimumScore = ValueInput<float>(nameof(MinimumScore), 0f);

            // Which query is being watched is a per-node question, so it is asked on the node. It used to
            // be a PlayerPrefs key read at every run: invisible on the canvas, all-or-nothing across every
            // agent in the scene, and carried between sessions by a value nothing in the project wrote.
            // A port also takes a connection, so "draw this one while the boss fight is running" is
            // authorable rather than a global switch someone has to remember to turn back off.
            DrawGizmo = ValueInput<bool>(nameof(DrawGizmo), false);

            SelectedPosition = ValueOutput<Vector3>(nameof(SelectedPosition), () => Select().Position);
            HasPosition = ValueOutput<bool>(nameof(HasPosition), () => Select().HasPosition);
        }

        /// <summary>
        /// The frame's selection, run on the first pull and reread by every later one. The cache is written
        /// only after a run completes, so a pull that throws leaves nothing stale behind and the next pull
        /// fails just as loudly.
        /// </summary>
        private TPSQueryResult Select()
        {
            var memory = Context.Memory<Memory>();
            if (memory.LastFrame == Time.frameCount) return memory.LastResult;

            var evaluator = ResolveEvaluator();

            if (evaluator == null) throw TPSQueryNodeChecks.NoEvaluator(NodeName, gameObject);

            var source = ResolveSource();

            if (source == null) throw TPSQueryNodeChecks.NoQuery(NodeName);

            var instance = memory.Built.For(source, evaluator, RowOpinions.None);
            var recorder = DrawGizmo.GetValue<bool>() ? TacticalPositionGizmo.RecorderIfEnabled : null;
            var request = new PositionQueryRequest(ResolveTarget(evaluator), MinimumScore.GetValue<float>());

            memory.LastResult = instance.Run(request, recorder);
            memory.LastFrame = Time.frameCount;

            return memory.LastResult;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            Context.Memory<Memory>().Built.Dispose();
        }

        // An unconnected target is the querier's own position, which centres target-relative knobs on the
        // querier instead of on a zero nobody chose.
        private Vector3 ResolveTarget(ITacticalAgent evaluator)
            => TargetPosition.hasValidConnection ? TargetPosition.GetValue<Vector3>() : evaluator.Position;

        private ITacticalAgent ResolveEvaluator()
        {
            if (Evaluator.hasValidConnection) return Evaluator.GetValue<ITacticalAgent>();

            return TPSAdapters.GetAgent(gameObject != null ? gameObject.GetComponent<GameEntity>() : null);
        }

        private IPositionQuerySource ResolveSource()
        {
            var connected = Query.hasValidConnection ? Query.GetValue<IPositionQuerySource>() : null;

            return TPSQuerySources.Resolve(connected, query);
        }

        private void KeepThePresetPickedOnThePort()
        {
            var picked = TPSQuerySources.InlinePickSavedOnThePort(this, nameof(Query));
            if (picked != null) query = picked;
        }

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            // A connected port is a runtime answer; there is nothing to check statically without pulling
            // the connection, which a problem scan must not do.
            if (Query.hasValidConnection) return;

            TPSQueryNodeChecks.CollectQueryProblems(query, into);
        }
    }
}
#endif

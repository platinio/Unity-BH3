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
    /// Asks the shared TPS scheduler for a position and waits for the answer: schedules on enter, runs until the
    /// query is done, then succeeds when the winner clears <see cref="MinimumScore"/> and fails otherwise. Exit
    /// cancels a query still running. The <see cref="TacticalPositionSelection"/> data node is the immediate path.
    /// </summary>
    [GraphCreateMenu("Navigation/Tactical Position Selection (Scheduled)")]
    public class ScheduledTacticalPositionSelection : GameplayNode
    {
        // A preset picked inline on the port while it took a query item; ResolveSource falls back to it whenever
        // nothing is connected.
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
        public ValueInput Priority { get; private set; }

        [DoNotSerialize]
        public ValueInput DrawGizmo { get; private set; }

        [DoNotSerialize]
        public ValueOutput SelectedPosition { get; private set; }

        [DoNotSerialize]
        public ValueOutput HasPosition { get; private set; }

        public override string NodeName => "Tactical Position Selection (Scheduled)";

        /// <summary>The instance this node built for its agent and keeps between decisions; null before the first.</summary>
        internal IPositionQueryInstance BuiltInstance => Context.Memory<Memory>().Built.Current;

        public override string Description =>
            "Schedules the selected tactical position query and runs until it answers; succeeds when the winner clears Minimum Score";

        public override Vector2 StartingSize => new(240.0f, 220.0f);

        private sealed class Memory
        {
            public TPSQueryHandle Handle;
            public TPSQueryResult Result = TPSQueryResult.None;
            public readonly BuiltPositionQuery Built = new();
        }

        protected override void Definition()
        {
            base.Definition();

            Query = ValueInput<IPositionQuerySource>(nameof(Query)).SafeToLeaveUnconnected();
            TargetPosition = ValueInput<Vector3>(nameof(TargetPosition)).SafeToLeaveUnconnected();
            Evaluator = ValueInput<ITacticalAgent>(nameof(Evaluator)).SafeToLeaveUnconnected();
            MinimumScore = ValueInput<float>(nameof(MinimumScore), 0f);
            Priority = ValueInput<TPSPriority>(nameof(Priority), TPSPriority.Normal);
            DrawGizmo = ValueInput<bool>(nameof(DrawGizmo), false);

            SelectedPosition = ValueOutput<Vector3>(nameof(SelectedPosition), () => Context.Memory<Memory>().Result.Position);
            HasPosition = ValueOutput<bool>(nameof(HasPosition), () => Context.Memory<Memory>().Result.HasPosition);
        }

        public override void OnEnter(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();
            memory.Handle.Dispose();
            memory.Result = TPSQueryResult.None;

            var agent = ResolveEvaluator(ctx);
            var instance = memory.Built.For(ResolveSource(ctx), agent, RowOpinions.None);
            var recorder = ctx.GetValue<bool>(DrawGizmo) ? TacticalPositionGizmo.RecorderIfEnabled : null;
            var request = new PositionQueryRequest(ResolveTarget(ctx, agent), ctx.GetValue<float>(MinimumScore));

            memory.Handle = instance.Schedule(
                TPSScheduler.Shared, request, ctx.GetValue<TPSPriority>(Priority), recorder);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();

            if (memory.Handle.IsCanceled) return ExecutionStatus.Failure;
            if (!memory.Handle.IsDone) return ExecutionStatus.Running;

            memory.Result = memory.Handle.Result;
            memory.Handle.Dispose();

            return memory.Result.HasPosition ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }

        public override void OnExit(BTContext ctx) => ctx.Memory<Memory>().Handle.Dispose();

        public override void AfterAdd()
        {
            KeepThePresetPickedOnThePort();
            base.AfterAdd();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            var memory = Context.Memory<Memory>();
            memory.Handle.Dispose();
            memory.Built.Dispose();
        }

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            if (Query.hasValidConnection) return;

            TPSQueryNodeChecks.CollectQueryProblems(query, into);
        }

        private Vector3 ResolveTarget(BTContext ctx, ITacticalAgent agent)
            => TargetPosition.hasValidConnection ? ctx.GetValue<Vector3>(TargetPosition) : agent.Position;

        private ITacticalAgent ResolveEvaluator(BTContext ctx)
        {
            var agent = Evaluator.hasValidConnection
                ? ctx.GetValue<ITacticalAgent>(Evaluator)
                : TPSAdapters.GetAgent(ctx.gameObject != null ? ctx.gameObject.GetComponent<GameEntity>() : null);

            if (agent == null) throw TPSQueryNodeChecks.NoEvaluator(NodeName, ctx.gameObject);

            return agent;
        }

        private IPositionQuerySource ResolveSource(BTContext ctx)
        {
            var connected = Query.hasValidConnection ? ctx.GetValue<IPositionQuerySource>(Query) : null;
            var source = TPSQuerySources.Resolve(connected, query);

            if (source == null) throw TPSQueryNodeChecks.NoQuery(NodeName);

            return source;
        }

        private void KeepThePresetPickedOnThePort()
        {
            var picked = TPSQuerySources.InlinePickSavedOnThePort(this, nameof(Query));
            if (picked != null) query = picked;
        }
    }
}
#endif

#if MODULE_TACTICAL_POSITION_SELECTION_EXIST
using System.Collections.Generic;
using ArcaneOnyx.AIEntities;
using ArcaneOnyx.GraphCore;
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

        public override string Description =>
            "Schedules the selected tactical position query and runs until it answers; succeeds when the winner clears Minimum Score";

        public override Vector2 StartingSize => new(240.0f, 220.0f);

        // The built query is kept for the agent's next decision, and disposed when the tree is released.
        private sealed class Memory
        {
            public TPSQueryHandle Handle;
            public TPSQueryResult Result = TPSQueryResult.None;
            public TacticalPositionSelectionQuery BuiltQuery;
            public TacticalPositionSelectionQueryItem BuiltFrom;
            public ITacticalAgent BuiltFor;
            public int BuiltAtVersion;
        }

        protected override void Definition()
        {
            base.Definition();

            Query = ValueInput<TacticalPositionSelectionQueryItem>(nameof(Query), null);
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
            var query = QueryFor(memory, ResolveQuery(ctx), agent);
            var recorder = ctx.GetValue<bool>(DrawGizmo) ? TacticalPositionGizmo.RecorderIfEnabled : null;

            memory.Handle = TPSScheduler.Shared.Schedule(query, agent, ResolveTarget(ctx, agent),
                ctx.GetValue<TPSPriority>(Priority), recorder, ctx.GetValue<float>(MinimumScore));
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();

            if (memory.Handle.IsCanceled) return ExecutionStatus.Failure;
            if (!memory.Handle.IsDone) return ExecutionStatus.Running;

            // The scheduler already applied MinimumScore: a rejected winner comes back as no position.
            memory.Result = memory.Handle.Result;
            memory.Handle.Dispose();

            return memory.Result.HasPosition ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }

        public override void OnExit(BTContext ctx) => ctx.Memory<Memory>().Handle.Dispose();

        public override void OnDestroy()
        {
            base.OnDestroy();

            var memory = Context.Memory<Memory>();
            memory.Handle.Dispose();
            ForgetBuiltQuery(memory);
        }

        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            if (Query.hasValidConnection) return;

            TPSQueryNodeChecks.CollectQueryProblems(Query.GetValue<TacticalPositionSelectionQueryItem>(), into);
        }

        // Built once per agent and preset; another preset or agent, or an edit to the preset, builds afresh.
        private static TacticalPositionSelectionQuery QueryFor(
            Memory memory, TacticalPositionSelectionQueryItem item, ITacticalAgent agent)
        {
            if (IsBuiltFor(memory, item, agent)) return memory.BuiltQuery;

            ForgetBuiltQuery(memory);
            memory.BuiltQuery = item.CreateTacticalPositionSelectionQuery(agent);
            memory.BuiltFrom = item;
            memory.BuiltFor = agent;
            memory.BuiltAtVersion = item.Query.Version;
            return memory.BuiltQuery;
        }

        private static bool IsBuiltFor(Memory memory, TacticalPositionSelectionQueryItem item, ITacticalAgent agent)
            => memory.BuiltQuery != null && memory.BuiltFrom == item && memory.BuiltFor == agent
               && memory.BuiltAtVersion == item.Query.Version;

        private static void ForgetBuiltQuery(Memory memory)
        {
            memory.BuiltQuery?.Dispose();
            memory.BuiltQuery = null;
            memory.BuiltFrom = null;
            memory.BuiltFor = null;
        }

        // Unconnected, the target is the agent itself.
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

        private TacticalPositionSelectionQueryItem ResolveQuery(BTContext ctx)
        {
            var item = ctx.GetValue<TacticalPositionSelectionQueryItem>(Query);

            if (item == null) throw TPSQueryNodeChecks.NoQuery(NodeName);

            return item;
        }
    }
}
#endif

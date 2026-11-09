#if MODULE_TACTICAL_POSITION_SELECTION_EXIST
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
    /// cancels a query still running. The answer usually lands one to three frames after entering; the
    /// <see cref="TacticalPositionSelection"/> data node is the immediate path.
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

        private sealed class Memory
        {
            public TPSQueryHandle Pending;
            public float MinimumScore;
            public TPSQueryResult Answer = TPSQueryResult.None;
            public bool HasUsableAnswer;
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

            SelectedPosition = ValueOutput<Vector3>(nameof(SelectedPosition), () => Context.Memory<Memory>().Answer.Position);
            HasPosition = ValueOutput<bool>(nameof(HasPosition), () => Context.Memory<Memory>().HasUsableAnswer);
        }

        public override void OnEnter(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();
            memory.Pending.Dispose();
            memory.Answer = TPSQueryResult.None;
            memory.HasUsableAnswer = false;
            memory.MinimumScore = ctx.GetValue<float>(MinimumScore);

            var agent = ResolveAgent(ctx);
            var item = ResolveQuery(ctx);
            var recorder = ctx.GetValue<bool>(DrawGizmo) ? TacticalPositionGizmo.RecorderIfEnabled : null;
            var priority = ctx.GetValue<TPSPriority>(Priority);

            // Unconnected, the target is the agent itself, as on the data node.
            var target = TargetPosition.hasValidConnection ? ctx.GetValue<Vector3>(TargetPosition) : agent.Position;

            memory.Pending = TPSScheduler.Shared.Schedule(item, agent, target, priority, recorder);
        }

        public override ExecutionStatus OnUpdate(BTContext ctx)
        {
            var memory = ctx.Memory<Memory>();

            if (memory.Pending.IsCanceled) return ExecutionStatus.Failure;
            if (!memory.Pending.IsDone) return ExecutionStatus.Running;

            memory.Answer = memory.Pending.Result;
            memory.HasUsableAnswer = IsUsable(memory.Answer, memory.MinimumScore);
            memory.Pending.Dispose();

            return memory.HasUsableAnswer ? ExecutionStatus.Success : ExecutionStatus.Failure;
        }

        public override void OnExit(BTContext ctx) => ctx.Memory<Memory>().Pending.Dispose();

        // The data node's rule: a veto never counts, and both comparisons are false for NaN.
        private static bool IsUsable(TPSQueryResult result, float minimumScore)
            => result.HasPosition && result.Score > 0f && result.Score >= minimumScore;

        private ITacticalAgent ResolveAgent(BTContext ctx)
        {
            var agent = Evaluator.hasValidConnection
                ? ctx.GetValue<ITacticalAgent>(Evaluator)
                : TPSAdapters.GetAgent(ctx.gameObject != null ? ctx.gameObject.GetComponent<GameEntity>() : null);

            if (agent == null)
            {
                throw new System.InvalidOperationException(
                    $"'{NodeName}' has no evaluator: nothing feeds the Evaluator port and " +
                    $"'{(ctx.gameObject != null ? ctx.gameObject.name : "(no object)")}' has no GameEntity to fall back to.");
            }

            return agent;
        }

        private TacticalPositionSelectionQueryItem ResolveQuery(BTContext ctx)
        {
            var item = ctx.GetValue<TacticalPositionSelectionQueryItem>(Query);

            if (item == null)
            {
                throw new System.InvalidOperationException(
                    $"'{NodeName}' has no query: nothing feeds the Query port and no inline value is set.");
            }

            return item;
        }
    }
}
#endif

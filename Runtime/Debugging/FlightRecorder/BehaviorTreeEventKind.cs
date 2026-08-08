namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// What a recorded event is. Deliberately a small closed set: a branch change in a behavior tree has
    /// only a few possible causes — a guard flipped, a child returned a status, or a composite fell through
    /// — so a recording that captures these can explain any transition without inference.
    /// </summary>
    public enum BehaviorTreeEventKind : byte
    {
        None = 0,

        /// <summary>A node started running.</summary>
        NodeEnter,

        /// <summary>A node stopped running. Carries the status it last returned.</summary>
        NodeExit,

        /// <summary>
        /// A guard turned false while its owner was running, so the owner returned Failure this tick.
        /// <see cref="BehaviorTreeEvent.RelatedGuid"/> is the guard that did it.
        /// </summary>
        NodeAborted,

        /// <summary>
        /// A guard was false when its owner was about to start, so the owner never entered.
        /// Distinct from <see cref="NodeAborted"/> because "never ran" and "ran and was killed" are
        /// different answers to "why didn't this happen", and the why-inspector words them differently.
        /// </summary>
        NodeSkipped,

        /// <summary>
        /// A guard's result changed. Recorded on transition only — guards evaluate every tick while their
        /// owner runs, and storing every evaluation would fill the buffer with the answer "still true".
        /// </summary>
        GuardEval,

        /// <summary>A variable was written. See <see cref="BehaviorTreeEvent.Key"/> and the value fields.</summary>
        VariableWrite,

        /// <summary>A <see cref="RunBehaviorTreeGraphNode"/> entered its sub-tree.</summary>
        TreePushed,

        /// <summary>A <see cref="RunBehaviorTreeGraphNode"/> left its sub-tree.</summary>
        TreePopped,

        /// <summary>
        /// A service attached to a node ticked. Nothing emits this yet — the Service node kind is spec 02.
        /// Reserved here so the recording schema does not change shape when it lands, and because the rule
        /// services must obey ("services tick before guards are evaluated") is only checkable against a
        /// recording that can show both in one tick, ordered.
        /// </summary>
        ServiceTick,
    }
}

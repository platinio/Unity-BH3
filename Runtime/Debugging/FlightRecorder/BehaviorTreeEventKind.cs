namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Written by name into every recording, so renaming a member stops older dumps importing.
    /// </summary>
    public enum BehaviorTreeEventKind : byte
    {
        None = 0,
        NodeEnter,
        NodeExit,
        NodeAborted,
        NodeSkipped,
        NodeTakenOver,
        GuardEval,
        VariableWrite,
        TreePushed,
        TreePopped,
        ServiceTick,
    }
}

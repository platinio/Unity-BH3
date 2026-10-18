namespace ArcaneOnyx.BehaviorTree.Debugging
{
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

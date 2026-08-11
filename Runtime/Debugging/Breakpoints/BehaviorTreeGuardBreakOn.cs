namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Which way a guard has to move to stop the editor.
    ///
    /// <para>
    /// Only transitions are on offer, because only transitions exist to break on: the recorder drops a guard
    /// result that matches the last one it saw, so a guard sitting false for four hundred ticks produces one
    /// event and not four hundred. That is the same reason a breakpoint here is usable at all — the naive
    /// version, stopping every tick a guard is false, would pause the editor on the frame you armed it and
    /// every frame after.
    /// </para>
    /// </summary>
    public enum BehaviorTreeGuardBreakOn
    {
        /// <summary>Any change of mind.</summary>
        EitherWay,

        /// <summary>Only false → true. The branch just became allowed.</summary>
        BecameTrue,

        /// <summary>Only true → false. This is the one that aborts a running branch.</summary>
        BecameFalse,
    }
}

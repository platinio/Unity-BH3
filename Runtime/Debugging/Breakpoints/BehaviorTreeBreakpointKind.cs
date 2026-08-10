namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// What a breakpoint watches. The three the spec asks for, and the discriminator the store indexes by —
    /// a node breakpoint and a guard breakpoint can name the same guid without meaning the same thing, since
    /// a <see cref="ConditionalExecution"/> is itself a node.
    /// </summary>
    public enum BehaviorTreeBreakpointKind
    {
        /// <summary>A node starting, stopping, being aborted or being refused entry.</summary>
        Node,

        /// <summary>A guard changing its mind.</summary>
        Guard,

        /// <summary>A variable being written, optionally only with a particular value.</summary>
        Variable,
    }
}

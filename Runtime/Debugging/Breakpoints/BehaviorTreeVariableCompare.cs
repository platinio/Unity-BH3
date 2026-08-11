namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// How a variable breakpoint's expected value is tested against the value being written.
    ///
    /// <para>
    /// There is no Unreal precedent to copy here: Blueprint breakpoints are unconditional, and the accepted
    /// workaround is to put a Branch node in the graph and break on its true pin — the condition lives in the
    /// asset rather than in the debugger. The precedent worth copying is a code debugger's, and those all
    /// offer this set.
    /// </para>
    ///
    /// <para>
    /// <b>The ordering operators are numeric only.</b> A behaviour tree variable can hold a
    /// <c>Vector3</c>, a <c>GameObject</c> or a list, and "less than" has no meaning for any of them.
    /// Falling back to lexicographic order would be defensible — that is what <c>CompareOrdinal</c> does —
    /// and it is the wrong call here, because "Zombie &lt; Skeleton" is a confident answer to a question
    /// nobody asked. Instead a non-numeric value leaves
    /// <see cref="BehaviorTreeBreakpoint.Diagnostic"/> saying so, which is the difference between a
    /// breakpoint that did not fire and a breakpoint you can see did not fire.
    /// </para>
    /// </summary>
    public enum BehaviorTreeVariableCompare
    {
        /// <summary>Any write at all, whatever the value. The default, and what an unset expected value means.</summary>
        Changed,

        Equals,

        NotEquals,

        LessThan,

        LessOrEqual,

        GreaterThan,

        GreaterOrEqual,

        /// <summary>
        /// The rendered value contains the expected text. More useful on a string than any ordering operator
        /// — "which state name has Attack in it" is a real question, "is this name less than that one" is not.
        /// </summary>
        Contains,
    }
}

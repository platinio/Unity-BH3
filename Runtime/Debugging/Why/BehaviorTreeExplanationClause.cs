namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// What a clause is doing in the explanation. The UI orders and styles by this; a reader skimming for the
    /// answer wants the cause, and a reader who does not believe the answer wants the evidence.
    /// </summary>
    public enum BehaviorTreeClauseRole : byte
    {
        /// <summary>The thing that made it happen.</summary>
        Cause = 0,

        /// <summary>The recorded fact the cause rests on — a write, a flip, a returned status.</summary>
        Evidence,

        /// <summary>True and useful, but not part of the causal chain: how long it ran, how often it repeats.</summary>
        Context,

        /// <summary>
        /// A limit on what the explanation can claim — the buffer is clipped, or the guard's inputs could not
        /// be resolved so a correlation is being offered rather than a cause.
        /// </summary>
        Caveat,
    }

    /// <summary>
    /// One sentence of an explanation, with the thing it points at kept separately from the words.
    /// </summary>
    public readonly struct BehaviorTreeExplanationClause
    {
        public readonly BehaviorTreeClauseRole Role;

        public readonly string Text;

        public readonly BehaviorTreeExplanationLink Link;

        public BehaviorTreeExplanationClause(BehaviorTreeClauseRole role, string text, BehaviorTreeExplanationLink link = default)
        {
            Role = role;
            Text = text;
            Link = link;
        }

        public override string ToString() => Text;
    }
}

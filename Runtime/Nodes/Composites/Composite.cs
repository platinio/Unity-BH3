namespace ArcaneOnyx.BehaviorTree
{
    public class Composite : ContainerNode
    {
        protected int currentExecutingChildIndex = 0;

        public override void OnAwake()
        {
            currentExecutingChildIndex = 0;
        }

        /// <summary>
        /// Whether a guard change on one child should move this composite's resume point, and to where.
        /// Returns false by default, so a composite that has not opted in pays nothing.
        ///
        /// <para>
        /// <b>This is a Composite-level contract rather than a Selector feature</b>, because the general
        /// question — <em>does a guard change on one child alter this composite's resume decision?</em> —
        /// has a different answer for each composite:
        /// </para>
        /// <list type="bullet">
        /// <item><see cref="Selector"/>: a higher-priority child becoming fully eligible <b>takes over</b>.</item>
        /// <item><see cref="Sequence"/>: the inverse. Its earlier children already <em>succeeded</em>, so a
        /// guard there is not a takeover bid but a sustained requirement — one going false while a later
        /// child runs should abort and fail the sequence. Specified, not yet built.</item>
        /// <item><see cref="ParallelSelector"/>: no reaction at all. Children already run concurrently, so
        /// there is no resume point to move.</item>
        /// </list>
        /// </summary>
        protected virtual bool TryChangeRunningChild(out int newChildIndex)
        {
            newChildIndex = currentExecutingChildIndex;
            return false;
        }

        /// <summary>
        /// The index of the first child in <c>[from, toExclusive)</c> that would enter right now, or -1.
        /// Walked in priority order, so the highest-priority eligible child wins by construction rather than
        /// by comparison.
        /// <para>
        /// Two gates, cheap one first. <see cref="BehaviorTreeNode.HasTakeOverGuard"/> is a cached bool and
        /// excludes every child that never opted in; only what survives that pays for
        /// <see cref="BehaviorTreeNode.WouldEnterNow"/>, which is a full entry-feasibility check and can run
        /// a graph.
        /// </para>
        /// </summary>
        protected int FirstChildThatWouldEnterNow(int from, int toExclusive)
        {
            var children = GetChildren();

            if (toExclusive > children.Count) toExclusive = children.Count;

            for (int i = from; i < toExclusive; i++)
            {
                var child = children[i];

                if (child == null || !child.HasTakeOverGuard) continue;
                if (child.WouldEnterNow()) return i;
            }

            return -1;
        }
    }
}

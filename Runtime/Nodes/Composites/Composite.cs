namespace ArcaneOnyx.BehaviorTree
{
    public class Composite : ContainerNode
    {
        /// <summary>
        /// What used to be <c>protected int currentExecutingChildIndex</c> plus <c>callOnEnter</c> on the
        /// two composites that had it. Migrated (spec 07 step 3).
        ///
        /// <para>
        /// This is the state that forces the clone: which branch a composite is part-way through is the
        /// most per-agent fact in a behavior tree, and every agent running the same Selector has a different
        /// answer. Sharing the node without moving this would have two hundred zombies fighting over one
        /// integer.
        /// </para>
        ///
        /// <para>
        /// Declared <c>protected</c> on the base so <see cref="Selector"/> and <see cref="Sequence"/> name
        /// the same type — one memory block per node, and both read it through
        /// <c>ctx.Memory&lt;CompositeMemory&gt;()</c>.
        /// </para>
        /// </summary>
        protected sealed class CompositeMemory
        {
            /// <summary>Which child this composite is resuming.</summary>
            public int Current;
        }

        // The old OnAwake() reset currentExecutingChildIndex to 0. Dropped rather than migrated: both
        // migrated composites set Current in OnEnter, so the reset was redundant -- and an OnAwake here
        // would claim this node's single memory slot for CompositeMemory on every composite, including the
        // ones not yet migrated, which would collide with whatever memory type they eventually want.

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
        ///
        /// <para>
        /// <b>"Resume" is every frame a branch runs longer than one tick</b>, which is the common case.
        /// <see cref="Selector.OnUpdate(BTContext)"/> starts its loop at the remembered index rather than at
        /// zero:
        /// </para>
        /// <code>
        /// frame 1  enter selector -> child0's guard is false, skipped -> child1 returns Running -> index = 1
        /// frame 2  OnUpdate again -> the loop starts at index 1
        ///          -> ticks child1 WITHOUT re-entering it, and WITHOUT reconsidering child0
        /// frame 3  the same
        /// </code>
        /// <para>
        /// Two separate reasons that is the right default. <b>Not re-entering</b> the running child, because
        /// <c>OnEnter</c> is not idempotent — re-entering a <c>WaitTime</c> resets its timer to full, an
        /// animation restarts, a <c>RandomChance</c> re-rolls, and any multi-frame action would hang forever.
        /// <b>Not reconsidering</b> the children above it, because until reactive guards existed there was no
        /// way for one of them to have become eligible in the meantime. That second half is what this hook
        /// changes, and only for children that opted in by carrying a take-over guard.
        /// </para>
        /// </summary>
        protected virtual bool TryChangeRunningChild(in BTContext ctx, out int newChildIndex)
        {
            newChildIndex = ctx.Memory<CompositeMemory>().Current;
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
        /// <para>
        /// Children come from the context, so the walk reads the baked plan on a shared tree and the node's
        /// own child list on a clone.
        /// </para>
        /// </summary>
        protected static int FirstChildThatWouldEnterNow(in BTContext ctx, int from, int toExclusive)
        {
            if (toExclusive > ctx.ChildCount) toExclusive = ctx.ChildCount;

            for (int i = from; i < toExclusive; i++)
            {
                var child = ctx.Child(i);

                if (child == null || !child.HasTakeOverGuard) continue;
                if (child.WouldEnterNow()) return i;
            }

            return -1;
        }
    }
}

using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A node that declares its ports from a copy of a contract held elsewhere, and can rebuild them when
    /// the two disagree.
    ///
    /// <para>
    /// Two nodes have this shape — the Function reader and the sub-tree caller — and they drift and are
    /// repaired identically; the capability is what the repair path depends on, so neither the editor's
    /// repair runner nor a future surface has to name either concrete node. Genuinely selective, unlike
    /// "can be wrong", which is why this is an interface while <c>CollectProblems</c> is a virtual on the
    /// base.
    /// </para>
    /// </summary>
    public interface IRefreshesContractPorts
    {
        /// <summary>
        /// How the remembered contract differs from what the referenced asset declares now, one line per
        /// difference and empty when they agree.
        /// </summary>
        List<string> DescribeContractDrift();

        /// <summary>
        /// Rebuilds the ports from the contract as it is now. Returns one line per connection the rebuild
        /// dropped — the part of a refresh the author did not ask for, which the caller owes them out loud.
        /// </summary>
        List<string> RefreshParameters();
    }
}

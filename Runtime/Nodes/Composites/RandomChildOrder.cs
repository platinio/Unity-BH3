using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The shuffle <see cref="RandomSelector"/> and <see cref="RandomSequence"/> share.
    ///
    /// <para>
    /// One place because it is one rule, and the two nodes had it written out twice — identical down to the
    /// LINQ. It is also the only part of either class that is not inherited, so a divergence between them
    /// would be a difference nobody declared.
    /// </para>
    /// </summary>
    internal static class RandomChildOrder
    {
        /// <summary>
        /// Shuffles in place, uniformly.
        ///
        /// <para>
        /// Fisher–Yates rather than <c>OrderBy(_ =&gt; Random.value)</c>: the LINQ form allocated an
        /// enumerator, a keys array and a fresh list on every exit of the node, and a container that exits
        /// once per pass does that for the whole life of the agent. This allocates nothing. It reorders the
        /// list the container already holds rather than replacing it, which is also why nothing else needs
        /// to hear about the change.
        /// </para>
        /// </summary>
        internal static void Shuffle(IList<BehaviorTreeNode> children)
        {
            if (children == null) return;

            // Down to 1, not 0: the last step would only ever swap element zero with itself.
            for (int index = children.Count - 1; index > 0; index--)
            {
                // Inclusive of index, or the element can never stay where it is and the result is a
                // derangement rather than a uniform permutation.
                int swap = Random.Range(0, index + 1);

                (children[index], children[swap]) = (children[swap], children[index]);
            }
        }
    }
}

using System.Linq;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="RandomSelector"/> and <see cref="RandomSequence"/> differ from their ordered parents in
    /// exactly one way: <see cref="ContainerNode.SortChildren"/> shuffles instead of sorting by canvas X.
    /// Everything else — the success/failure rule, the single-tick descent — is inherited and must stay
    /// inherited.
    /// <para>
    /// The risk with a shuffle is that it quietly loses or duplicates a child, which would make a branch
    /// unreachable in a way that only shows up as an agent occasionally doing nothing. That invariant is
    /// what these pin, rather than the ordering itself, which is random by design.
    /// </para>
    /// </summary>
    [TestFixture]
    public class RandomOrderCompositeTests
    {
        [Test]
        public void RandomSelector_ShufflingKeepsEveryChildExactlyOnce()
        {
            var children = Enumerable.Range(0, 8).Select(_ => new ScriptedNode()).ToArray();
            var selector = new RandomSelector().WithChildren(children);

            for (int shuffle = 0; shuffle < 20; shuffle++)
            {
                selector.SortChildren();

                var actual = selector.GetChildren();
                Assert.AreEqual(children.Length, actual.Count, "A shuffle must not change the child count.");
                CollectionAssert.AreEquivalent(children, actual,
                    "A shuffle must be a permutation — no child dropped, none duplicated.");
            }
        }

        [Test]
        public void RandomSequence_ShufflingKeepsEveryChildExactlyOnce()
        {
            var children = Enumerable.Range(0, 8).Select(_ => new ScriptedNode()).ToArray();
            var sequence = new RandomSequence().WithChildren(children);

            for (int shuffle = 0; shuffle < 20; shuffle++)
            {
                sequence.SortChildren();

                var actual = sequence.GetChildren();
                Assert.AreEqual(children.Length, actual.Count, "A shuffle must not change the child count.");
                CollectionAssert.AreEquivalent(children, actual,
                    "A shuffle must be a permutation — no child dropped, none duplicated.");
            }
        }

        [Test]
        public void RandomSelector_StillSucceedsOnTheFirstSuccessWhateverTheOrder()
        {
            // Every child succeeds, so the result cannot depend on which one the shuffle put first.
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var selector = new RandomSelector().WithChildren(a, b);

            selector.SortChildren();

            Assert.AreEqual(ExecutionStatus.Success, selector.RunToCompletion());
            Assert.AreEqual(1, a.UpdateCalls + b.UpdateCalls,
                "A selector stops at the first child that succeeds, whichever one the shuffle chose.");
        }

        [Test]
        public void RandomSelector_FailsOnlyWhenEveryChildFails()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Failure);
            var selector = new RandomSelector().WithChildren(a, b);

            selector.SortChildren();

            Assert.AreEqual(ExecutionStatus.Failure, selector.RunToCompletion());
            Assert.AreEqual(1, a.UpdateCalls, "Both children must be tried before the node gives up.");
            Assert.AreEqual(1, b.UpdateCalls);
        }

        [Test]
        public void RandomSequence_FailsOnTheFirstFailureWhateverTheOrder()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Failure);
            var sequence = new RandomSequence().WithChildren(a, b);

            sequence.SortChildren();

            Assert.AreEqual(ExecutionStatus.Failure, sequence.RunToCompletion());
            Assert.AreEqual(1, a.UpdateCalls + b.UpdateCalls,
                "A sequence stops at the first child that fails, whichever one the shuffle chose.");
        }

        [Test]
        public void RandomSequence_SucceedsOnlyWhenEveryChildSucceeds()
        {
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var sequence = new RandomSequence().WithChildren(a, b);

            sequence.SortChildren();

            Assert.AreEqual(ExecutionStatus.Success, sequence.RunToCompletion());
            Assert.AreEqual(1, a.UpdateCalls);
            Assert.AreEqual(1, b.UpdateCalls);
        }

        /// <summary>
        /// The shuffle actually reorders.
        ///
        /// <para>
        /// The two permutation tests above are satisfied by a shuffle that returns the list untouched, which
        /// is a real way to get Fisher-Yates wrong: draw the swap index from the wrong range and elements can
        /// end up unable to move, or unable to stay. Eight children reshuffled twenty times land in their
        /// original order every single time with probability 40320 to the power of -20, so a run that never
        /// reorders is a broken shuffle rather than luck.
        /// </para>
        /// </summary>
        [Test]
        public void RandomSelector_ShufflingDoesNotLeaveTheOrderAlone()
        {
            var children = Enumerable.Range(0, 8).Select(_ => new ScriptedNode()).ToArray();
            var selector = new RandomSelector().WithChildren(children);

            bool reordered = false;

            for (int shuffle = 0; shuffle < 20 && !reordered; shuffle++)
            {
                selector.SortChildren();
                reordered = !selector.GetChildren().SequenceEqual(children);
            }

            Assert.IsTrue(reordered,
                "Twenty shuffles of eight children never changed the order, so the shuffle is not shuffling.");
        }

    }
}

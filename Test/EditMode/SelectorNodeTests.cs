using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of the <see cref="Selector"/> composite: run children left to right, succeed on the
    /// first success, fail only when every child fails — the mirror image of <see cref="Sequence"/>.
    /// <para>
    /// The descent must also complete within a single tick. A behavior tree is a decision structure, not a
    /// state machine: each tick asks what the agent should be doing given the world right now, so an answer
    /// that took a frame per rejected branch would be acting on a stale world by the time it arrived.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SelectorNodeTests
    {
        [Test]
        public void FirstChildSucceeds_SelectorSucceedsAndSkipsRemaining()
        {
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var selector = new Selector().WithChildren(a, b);

            var status = selector.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Success, status);
            Assert.AreEqual(0, b.UpdateCalls, "Selector must stop at the first child that succeeds.");
        }

        [Test]
        public void FirstChildFails_SelectorFallsThroughToNext()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var selector = new Selector().WithChildren(a, b);

            var status = selector.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Success, status);
            Assert.AreEqual(1, b.UpdateCalls, "Selector must try the next child after a failure.");
        }

        [Test]
        public void AllChildrenFail_SelectorFails()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Failure);
            var selector = new Selector().WithChildren(a, b);

            var status = selector.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Failure, status);
        }

        /// <summary>
        /// The regression this fixture exists for. Nine failing branches used to cost nine frames, because
        /// the selector entered the next child and returned Running instead of trying it.
        /// </summary>
        [Test]
        public void TheLastBranchIsReachedOnTheFirstTick()
        {
            var children = new BehaviorTreeNode[10];
            for (int i = 0; i < 9; i++)
            {
                children[i] = new ScriptedNode(ExecutionStatus.Failure);
            }

            var last = new ScriptedNode(ExecutionStatus.Success);
            children[9] = last;

            var selector = new Selector().WithChildren(children);

            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal(),
                "One tick must descend all the way to the branch that runs, not advance one child per frame.");
            Assert.AreEqual(1, last.UpdateCalls, "The branch that runs must have been ticked within that tick.");
        }

        [Test]
        public void EveryChildFailing_ResolvesToFailureOnTheFirstTick()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Failure);
            var c = new ScriptedNode(ExecutionStatus.Failure);
            var selector = new Selector().WithChildren(a, b, c);

            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Failure, selector.OnUpdateInternal(),
                "A selector with nothing left to try reports Failure immediately.");
            Assert.AreEqual(1, c.UpdateCalls, "Every child must have been tried inside that one tick.");
        }

        [Test]
        public void EachTriedChild_IsEnteredAndExitedExactlyOnce()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var selector = new Selector().WithChildren(a, b);

            selector.RunToCompletion();

            Assert.AreEqual(1, a.EnterCalls, "Descending faster must not enter a child twice.");
            Assert.AreEqual(1, a.ExitCalls);
            Assert.AreEqual(1, b.EnterCalls);
            Assert.AreEqual(1, b.ExitCalls);
        }

        /// <summary>
        /// A child that is still Running must be ticked again next frame but <em>not</em> re-entered.
        /// Re-entering it would re-run OnEnter every frame — resetting a WaitTime's timer forever,
        /// restarting an animation, re-rolling a RandomChance — so the branch could never finish.
        /// </summary>
        [Test]
        public void ARunningChild_IsTickedAgainButNotReEntered()
        {
            var child = new ScriptedNode().Returns(
                ExecutionStatus.Running, ExecutionStatus.Running, ExecutionStatus.Success);
            var selector = new Selector().WithChildren(child);

            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal());
            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal());
            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal());

            Assert.AreEqual(1, child.EnterCalls, "A running child must be entered exactly once.");
            Assert.AreEqual(3, child.UpdateCalls, "But ticked on every frame it stays Running.");
        }

        [Test]
        public void RunningChild_OwnsTheFrameThenTheSelectorFallsThroughWhenItFails()
        {
            var a = new ScriptedNode().Returns(ExecutionStatus.Running, ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var selector = new Selector().WithChildren(a, b);

            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal(),
                "A child that is genuinely still working is the one thing that ends a tick.");
            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal(),
                "Once it fails the selector falls through and resolves the next branch in that same tick.");
            Assert.AreEqual(1, b.UpdateCalls);
        }

        /// <summary>
        /// A Selector is an OR, so an empty one is an empty OR — Failure, the same answer
        /// <see cref="ParallelSelector"/> gives. Succeeding here would report that the branch handled the
        /// situation when it ran nothing, which hides a mis-authored tree behind a green result.
        /// </summary>
        [Test]
        public void EmptySelector_Fails()
        {
            var selector = new Selector();

            Assert.AreEqual(ExecutionStatus.Failure, selector.OnUpdate());
        }
    }
}

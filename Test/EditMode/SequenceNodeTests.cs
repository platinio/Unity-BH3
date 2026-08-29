using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of the <see cref="Sequence"/> composite: run children left to right, stop on the first
    /// failure, succeed only when every child succeeds. These are the contract the README advertises and
    /// the rule designers rely on when laying out a branch.
    /// <para>
    /// As with <see cref="Selector"/>, the walk happens inside one tick — only a child that returns Running
    /// ends the frame.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SequenceNodeTests
    {
        [Test]
        public void AllChildrenSucceed_SequenceSucceeds()
        {
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var sequence = new Sequence().WithChildren(a, b);

            var status = sequence.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Success, status);
        }

        [Test]
        public void FirstChildFails_SequenceFailsAndSkipsRemaining()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var sequence = new Sequence().WithChildren(a, b);

            var status = sequence.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Failure, status);
            Assert.AreEqual(0, b.UpdateCalls, "Sequence must not tick children past the one that failed.");
        }

        [Test]
        public void EachChild_IsEnteredAndExitedExactlyOnce()
        {
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var sequence = new Sequence().WithChildren(a, b);

            sequence.RunToCompletion();

            Assert.AreEqual(1, a.EnterCalls);
            Assert.AreEqual(1, a.ExitCalls);
            Assert.AreEqual(1, b.EnterCalls);
            Assert.AreEqual(1, b.ExitCalls);
        }

        /// <summary>
        /// The mirror of the selector regression: a sequence of instant actions used to cost one frame per
        /// action, so a five-step setup took five frames to run.
        /// </summary>
        [Test]
        public void EveryChildSucceeding_CompletesOnTheFirstTick()
        {
            var children = new BehaviorTreeNode[10];
            for (int i = 0; i < 10; i++)
            {
                children[i] = new ScriptedNode(ExecutionStatus.Success);
            }

            var last = (ScriptedNode) children[9];
            var sequence = new Sequence().WithChildren(children);

            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, sequence.OnUpdateInternal(),
                "A sequence of instant actions resolves in the tick that started it.");
            Assert.AreEqual(1, last.UpdateCalls, "The final child must have run inside that tick.");
        }

        /// <summary>
        /// The mirror of the Selector case: a Running child is ticked again next frame but never
        /// re-entered, or its OnEnter would run every frame and the branch could never finish.
        /// </summary>
        [Test]
        public void ARunningChild_IsTickedAgainButNotReEntered()
        {
            var child = new ScriptedNode().Returns(
                ExecutionStatus.Running, ExecutionStatus.Running, ExecutionStatus.Success);
            var sequence = new Sequence().WithChildren(child);

            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal());
            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal());
            Assert.AreEqual(ExecutionStatus.Success, sequence.OnUpdateInternal());

            Assert.AreEqual(1, child.EnterCalls, "A running child must be entered exactly once.");
            Assert.AreEqual(3, child.UpdateCalls, "But ticked on every frame it stays Running.");
        }

        [Test]
        public void RunningChild_OwnsTheFrameThenTheSequenceCompletesWhenItSucceeds()
        {
            // a stays Running for one tick, then succeeds; b then succeeds.
            var a = new ScriptedNode().Returns(ExecutionStatus.Running, ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var sequence = new Sequence().WithChildren(a, b);

            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "While the first child is Running the sequence stays Running.");
            Assert.AreEqual(ExecutionStatus.Success, sequence.OnUpdateInternal(),
                "The first child succeeds and the rest of the sequence runs in that same tick.");
        }

        [Test]
        public void EmptySequence_Succeeds()
        {
            var sequence = new Sequence();

            // See EmptySelector_Fails: migrated nodes are exercised through OnUpdateInternal.
            Assert.AreEqual(ExecutionStatus.Success, sequence.OnUpdateInternal());
        }
    }
}

using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of the <see cref="Sequence"/> composite: run children left to right, stop on the first
    /// failure, succeed only when every child succeeds. These are the contract the README advertises and
    /// the rule designers rely on when laying out a branch.
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

        [Test]
        public void RunningChild_KeepsSequenceRunningUntilItSucceeds()
        {
            // a stays Running for one tick, then succeeds; b then succeeds.
            var a = new ScriptedNode().Returns(ExecutionStatus.Running, ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var sequence = new Sequence().WithChildren(a, b);

            sequence.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "While the first child is Running the sequence stays Running.");
            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "First child just succeeded; sequence advances to the second child and stays Running.");
            Assert.AreEqual(ExecutionStatus.Success, sequence.OnUpdateInternal(),
                "Second child succeeds; sequence completes.");
        }

        [Test]
        public void EmptySequence_Succeeds()
        {
            var sequence = new Sequence();

            Assert.AreEqual(ExecutionStatus.Success, sequence.OnUpdate());
        }
    }
}

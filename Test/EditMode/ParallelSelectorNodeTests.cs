using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of <see cref="ParallelSelector"/>: every child is ticked each frame; the node succeeds as
    /// soon as any child succeeds and fails only once every child has failed. The mirror of
    /// <see cref="ParallelSequence"/>, and the parallel counterpart of <see cref="Selector"/>.
    /// </summary>
    [TestFixture]
    public class ParallelSelectorNodeTests
    {
        [Test]
        public void AnyChildSucceeds_ParallelSelectorSucceeds()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var parallel = new ParallelSelector().WithChildren(a, b);

            Assert.AreEqual(ExecutionStatus.Success, parallel.RunToCompletion());
        }

        [Test]
        public void AllChildrenFail_ParallelSelectorFails()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Failure);
            var parallel = new ParallelSelector().WithChildren(a, b);

            Assert.AreEqual(ExecutionStatus.Failure, parallel.RunToCompletion());
        }

        [Test]
        public void AllChildren_AreEnteredOnEnter()
        {
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Failure);
            var parallel = new ParallelSelector().WithChildren(a, b);

            parallel.OnNodeEnter();

            Assert.AreEqual(1, a.EnterCalls);
            Assert.AreEqual(1, b.EnterCalls);
        }

        [Test]
        public void AChildThatAlreadyFailed_IsNotTickedAgain()
        {
            // a fails on the first frame; b needs two frames before it succeeds. The parallel must keep
            // ticking b while leaving the settled a alone.
            var a = new ScriptedNode(ExecutionStatus.Failure);
            var b = new ScriptedNode().Returns(ExecutionStatus.Running, ExecutionStatus.Success);
            var parallel = new ParallelSelector().WithChildren(a, b);

            parallel.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, parallel.OnUpdateInternal(),
                "One child failed but another is still working, so the node is not resolved yet.");
            Assert.AreEqual(ExecutionStatus.Success, parallel.OnUpdateInternal(),
                "Succeeds as soon as any child succeeds.");

            Assert.AreEqual(1, a.UpdateCalls, "A child that already failed must not be re-ticked.");
        }

        /// <summary>
        /// An empty parallel selector is an empty OR, so it fails — the same answer <see cref="Selector"/>
        /// gives. The two are pinned together deliberately: they are the same operator.
        /// </summary>
        [Test]
        public void EmptyParallelSelector_Fails()
        {
            var parallel = new ParallelSelector();

            Assert.AreEqual(ExecutionStatus.Failure, parallel.RunToCompletion());
        }
    }
}

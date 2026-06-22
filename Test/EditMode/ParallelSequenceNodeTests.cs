using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of <see cref="ParallelSequence"/>: every child is ticked each frame; the node fails as
    /// soon as any child fails and succeeds only once every child has succeeded.
    /// </summary>
    [TestFixture]
    public class ParallelSequenceNodeTests
    {
        [Test]
        public void AllChildrenSucceed_ParallelSucceeds()
        {
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var parallel = new ParallelSequence().WithChildren(a, b);

            Assert.AreEqual(ExecutionStatus.Success, parallel.RunToCompletion());
        }

        [Test]
        public void AnyChildFails_ParallelFails()
        {
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Failure);
            var parallel = new ParallelSequence().WithChildren(a, b);

            Assert.AreEqual(ExecutionStatus.Failure, parallel.RunToCompletion());
        }

        [Test]
        public void AllChildren_AreTickedEveryFrameWhileRunning()
        {
            // a needs two frames to finish; b finishes on the first frame. The parallel should keep
            // ticking until the slower child resolves, and stay Running in the meantime.
            var a = new ScriptedNode().Returns(ExecutionStatus.Running, ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var parallel = new ParallelSequence().WithChildren(a, b);

            parallel.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, parallel.OnUpdateInternal(),
                "Still Running while one child has not finished.");
            Assert.AreEqual(ExecutionStatus.Success, parallel.OnUpdateInternal(),
                "Succeeds once every child has succeeded.");

            // b completed on frame one and must not be ticked again after it succeeded.
            Assert.AreEqual(1, b.UpdateCalls, "A child that already succeeded should not be re-ticked.");
        }

        [Test]
        public void AllChildren_AreEnteredOnEnter()
        {
            var a = new ScriptedNode(ExecutionStatus.Success);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var parallel = new ParallelSequence().WithChildren(a, b);

            parallel.OnNodeEnter();

            Assert.AreEqual(1, a.EnterCalls);
            Assert.AreEqual(1, b.EnterCalls);
        }
    }
}

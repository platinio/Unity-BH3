using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of the <see cref="Selector"/> composite: run children left to right, succeed on the
    /// first success, fail only when every child fails — the mirror image of <see cref="Sequence"/>.
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

        [Test]
        public void RunningChild_KeepsSelectorRunningUntilItResolves()
        {
            var a = new ScriptedNode().Returns(ExecutionStatus.Running, ExecutionStatus.Failure);
            var b = new ScriptedNode(ExecutionStatus.Success);
            var selector = new Selector().WithChildren(a, b);

            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal());
            Assert.AreEqual(ExecutionStatus.Running, selector.OnUpdateInternal(),
                "First child failed; selector advances to the second child and stays Running.");
            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal());
        }

        [Test]
        public void EmptySelector_Succeeds()
        {
            var selector = new Selector();

            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdate());
        }
    }
}

using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    [TestFixture]
    public class WaitUntilTests
    {
        private static WaitUntil WaitingFor(bool value)
        {
            var wait = new WaitUntil();
            wait.Define();
            wait.Value.SetDefaultValue(value);

            return wait;
        }

        [Test]
        public void AFalseInput_KeepsRunning()
        {
            Assert.AreEqual(ExecutionStatus.Running, WaitingFor(false).OnUpdate());
        }

        [Test]
        public void ATrueInput_Succeeds()
        {
            Assert.AreEqual(ExecutionStatus.Success, WaitingFor(true).OnUpdate());
        }

        [Test]
        public void AnUnconnectedInput_WaitsRatherThanThrowing()
        {
            var wait = new WaitUntil();
            wait.Define();

            Assert.AreEqual(ExecutionStatus.Running, wait.OnUpdate());
        }

        [Test]
        public void ItSucceedsOnTheTickTheInputTurnsTrue()
        {
            var wait = WaitingFor(false);
            wait.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, wait.OnUpdateInternal());
            Assert.AreEqual(ExecutionStatus.Running, wait.OnUpdateInternal());

            wait.Value.SetDefaultValue(true);

            Assert.AreEqual(ExecutionStatus.Success, wait.OnUpdateInternal());
        }

        [Test]
        public void ItHoldsAParallelSelectorOpenUntilTheInputTurnsTrue()
        {
            var wait = WaitingFor(false);
            var parallel = new ParallelSelector().WithChildren(wait);
            parallel.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Running, parallel.OnUpdateInternal());
            Assert.AreEqual(ExecutionStatus.Running, parallel.OnUpdateInternal());

            wait.Value.SetDefaultValue(true);

            Assert.AreEqual(ExecutionStatus.Success, parallel.OnUpdateInternal());
        }
    }
}

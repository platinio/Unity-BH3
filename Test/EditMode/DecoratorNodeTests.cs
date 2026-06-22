using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Behaviour of the decorator nodes, each of which wraps a single child and rewrites how its result
    /// is interpreted. These pin the contracts the README documents for each decorator.
    /// </summary>
    [TestFixture]
    public class DecoratorNodeTests
    {
        [Test]
        public void ReturnSuccess_TurnsChildFailureIntoSuccess()
        {
            var child = new ScriptedNode(ExecutionStatus.Failure);
            var decorator = new ReturnSuccess().WithChildren(child);

            Assert.AreEqual(ExecutionStatus.Success, decorator.RunToCompletion());
        }

        [Test]
        public void ReturnSuccess_EmptyDecoratorSucceeds()
        {
            Assert.AreEqual(ExecutionStatus.Success, new ReturnSuccess().OnUpdate());
        }

        [Test]
        public void ReturnFailure_TurnsChildSuccessIntoFailure()
        {
            var child = new ScriptedNode(ExecutionStatus.Success);
            var decorator = new ReturnFailure().WithChildren(child);

            Assert.AreEqual(ExecutionStatus.Failure, decorator.RunToCompletion());
        }

        [Test]
        public void ReturnFailure_EmptyDecoratorFails()
        {
            Assert.AreEqual(ExecutionStatus.Failure, new ReturnFailure().OnUpdate());
        }

        [Test]
        public void UntilSuccess_RepeatsChildUntilItSucceeds()
        {
            var child = new ScriptedNode().Returns(
                ExecutionStatus.Failure, ExecutionStatus.Failure, ExecutionStatus.Success);
            var decorator = new UntilSuccess().WithChildren(child);

            Assert.AreEqual(ExecutionStatus.Success, decorator.RunToCompletion());
            Assert.AreEqual(3, child.UpdateCalls, "Child should be re-run until it finally succeeds.");
        }

        [Test]
        public void UntilFailure_RepeatsChildUntilItFails()
        {
            var child = new ScriptedNode().Returns(
                ExecutionStatus.Success, ExecutionStatus.Success, ExecutionStatus.Failure);
            var decorator = new UntilFailure().WithChildren(child);

            Assert.AreEqual(ExecutionStatus.Success, decorator.RunToCompletion());
            Assert.AreEqual(3, child.UpdateCalls, "Child should be re-run until it finally fails.");
        }

        [Test]
        public void Repeater_NeverCompletesAndKeepsReEnteringChild()
        {
            var child = new ScriptedNode(ExecutionStatus.Success);
            var repeater = new Repeater().WithChildren(child);

            repeater.OnNodeEnter();

            const int ticks = 5;
            for (int i = 0; i < ticks; i++)
            {
                Assert.AreEqual(ExecutionStatus.Running, repeater.OnUpdateInternal(),
                    "Repeater always reports Running, no matter what the child returns.");
            }

            // Entered once by OnEnter, then re-entered after each completing tick.
            Assert.AreEqual(ticks + 1, child.EnterCalls, "Repeater should restart the child on every completion.");
        }

        [Test]
        public void Repeater_EmptyDecoratorSucceeds()
        {
            Assert.AreEqual(ExecutionStatus.Success, new Repeater().OnUpdate());
        }
    }
}

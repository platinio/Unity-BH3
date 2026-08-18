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

            // One entry per tick, because the tick is what enters the child -- see ContainerNode.TickChild.
            // It used to be entered once up front and then restarted at the end of each completing tick,
            // which is the same count offset by one and left the child sitting entered across the gap
            // between two frames.
            Assert.AreEqual(ticks, child.EnterCalls, "Repeater should restart the child on every completion.");
        }

        [Test]
        public void Repeater_EmptyDecoratorSucceeds()
        {
            Assert.AreEqual(ExecutionStatus.Success, new Repeater().OnUpdate());
        }

        [Test]
        public void Cooldown_RunsChildOnFirstActivation()
        {
            var child = new ScriptedNode(ExecutionStatus.Success);
            var cooldown = new Cooldown().WithChildren(child);
            cooldown.Define();

            Assert.AreEqual(ExecutionStatus.Success, cooldown.RunToCompletion());
            Assert.AreEqual(1, child.UpdateCalls, "A cooldown that has never fired must let the child through.");
        }

        [Test]
        public void Cooldown_BlocksChildWhileRecharging()
        {
            var child = new ScriptedNode(ExecutionStatus.Success);
            var cooldown = new Cooldown().WithChildren(child);
            cooldown.Define();

            cooldown.RunToCompletion();
            int updatesBefore = child.UpdateCalls;

            // Edit mode advances no game time between these two runs, so the cooldown is still active.
            Assert.AreEqual(ExecutionStatus.Failure, cooldown.RunToCompletion());
            Assert.AreEqual(updatesBefore, child.UpdateCalls, "Child must not tick while the decorator is cooling down.");
            Assert.AreEqual(1, child.EnterCalls, "Child must not be entered while the decorator is cooling down.");
        }

        [Test]
        public void RandomChance_AlwaysRunsChildAtFullChance()
        {
            var child = new ScriptedNode(ExecutionStatus.Success);
            var decorator = new RandomChance().WithChildren(child);
            decorator.Define();
            decorator.Chance.SetDefaultValue(1.0f);

            Assert.AreEqual(ExecutionStatus.Success, decorator.RunToCompletion());
            Assert.AreEqual(1, child.UpdateCalls, "A chance of 1 must always pass the roll.");
        }

        [Test]
        public void RandomChance_NeverRunsChildAtZeroChance()
        {
            var child = new ScriptedNode(ExecutionStatus.Success);
            var decorator = new RandomChance().WithChildren(child);
            decorator.Define();
            decorator.Chance.SetDefaultValue(0.0f);

            Assert.AreEqual(ExecutionStatus.Failure, decorator.RunToCompletion());
            Assert.AreEqual(0, child.UpdateCalls, "A chance of 0 must never tick the child.");
            Assert.AreEqual(0, child.EnterCalls, "A chance of 0 must never enter the child.");
        }
    }
}

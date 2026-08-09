using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="BooleanCondition"/> is the bridge that lets any boolean-producing value node act as a
    /// decision inside a Selector without knowing it is being used as a test. It is the condition-node
    /// counterpart of <see cref="BooleanConditionalExecution"/>, which
    /// <see cref="ConditionalExecutionTests"/> covers as a guard.
    /// </summary>
    [TestFixture]
    public class BooleanConditionTests
    {
        private static BooleanCondition ConditionReading(bool value)
        {
            var condition = new BooleanCondition();
            condition.Define();
            condition.Value.SetDefaultValue(value);

            return condition;
        }

        [Test]
        public void ATrueInput_Succeeds()
        {
            Assert.AreEqual(ExecutionStatus.Success, ConditionReading(true).OnUpdate());
        }

        [Test]
        public void AFalseInput_Fails()
        {
            Assert.AreEqual(ExecutionStatus.Failure, ConditionReading(false).OnUpdate());
        }

        /// <summary>
        /// The port declares a default, so an unconnected condition reads false rather than throwing —
        /// the safe direction for a test, and what lets <c>bt_verify</c> stay quiet about it.
        /// </summary>
        [Test]
        public void AnUnconnectedInput_ReadsFalseRatherThanThrowing()
        {
            var condition = new BooleanCondition();
            condition.Define();

            Assert.AreEqual(ExecutionStatus.Failure, condition.OnUpdate());
        }

        [Test]
        public void ItDrivesASelectorLikeAnyOtherChild()
        {
            // The point of the node: a Selector falls past a false condition to the next branch, in the
            // same tick, exactly as it would past any other failing child.
            var fallback = new ScriptedNode(ExecutionStatus.Success);
            var selector = new Selector().WithChildren(ConditionReading(false), fallback);

            selector.OnNodeEnter();

            Assert.AreEqual(ExecutionStatus.Success, selector.OnUpdateInternal());
            Assert.AreEqual(1, fallback.UpdateCalls);
        }
    }
}

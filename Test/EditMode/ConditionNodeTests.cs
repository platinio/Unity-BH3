using ArcaneOnyx.GraphCore;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <see cref="Condition"/> maps a boolean <see cref="Condition.Evaluate"/> onto the execution status
    /// the tree understands — true becomes Success, false becomes Failure. This is what lets conditions
    /// act as guards inside selectors and decorators.
    /// </summary>
    [TestFixture]
    public class ConditionNodeTests
    {
        [Test]
        public void EvaluatingTrue_ReturnsSuccess()
        {
            var condition = new FixedCondition(true);

            Assert.AreEqual(ExecutionStatus.Success, condition.OnUpdate());
        }

        [Test]
        public void EvaluatingFalse_ReturnsFailure()
        {
            var condition = new FixedCondition(false);

            Assert.AreEqual(ExecutionStatus.Failure, condition.OnUpdate());
        }

        [Test]
        public void BaseCondition_DefaultsToFailure()
        {
            // The default Evaluate() returns false, so a condition with no logic must fail rather than
            // silently pass and let a guarded branch run.
            var condition = new Condition();

            Assert.IsFalse(condition.Evaluate());
            Assert.AreEqual(ExecutionStatus.Failure, condition.OnUpdate());
        }
    }
}

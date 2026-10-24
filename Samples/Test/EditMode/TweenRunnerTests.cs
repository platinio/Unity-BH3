using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class TweenRunnerTests
    {
        private const float OneSecond = 1f;
        private const float OneFrame = 1f / 60f;

        private GameObject firstOwner;
        private GameObject secondOwner;
        private int timesApplied;

        [SetUp]
        public void SetUp()
        {
            firstOwner = new GameObject("First Owner");
            secondOwner = new GameObject("Second Owner");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(firstOwner);
            Object.DestroyImmediate(secondOwner);
            TweenRunnerCleanup.DestroyEveryRunner();
        }

        private Tween StartCountedTween(GameObject owner)
        {
            var tween = new Tween(owner, OneSecond, Easing.Linear, _ => timesApplied++);
            TweenRunner.Add(tween);
            return tween;
        }

        [Test]
        public void KillingAnOwnersTweensLeavesOtherOwnersAnimating()
        {
            StartCountedTween(firstOwner);
            StartCountedTween(secondOwner);

            TweenRunner.KillAll(firstOwner);

            Assert.IsFalse(TweenRunner.IsAnimating(firstOwner));
            Assert.IsTrue(TweenRunner.IsAnimating(secondOwner));
        }

        [Test]
        public void ATweenWhoseOwnerWasDestroyedIsDroppedByTheNextAdvance()
        {
            Tween orphan = StartCountedTween(firstOwner);
            Object.DestroyImmediate(firstOwner);

            TweenRunner.Advance(OneFrame);

            Assert.IsFalse(orphan.IsPlaying);
            Assert.AreEqual(0, timesApplied);
        }
    }
}

using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class TweenTests
    {
        private const float OneSecond = 1f;

        private GameObject owner;
        private float appliedProgress;
        private Tween tween;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Owner");
            tween = new Tween(owner, OneSecond, Easing.Linear, progress => appliedProgress = progress);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(owner);

        [Test]
        public void KeepsPlayingUntilItsElapsedTimeReachesItsDuration()
        {
            tween.Advance(0.75f);
            Assert.IsTrue(tween.IsPlaying);

            tween.Advance(0.25f);
            Assert.IsFalse(tween.IsPlaying);
        }

        [Test]
        public void AppliesFullProgressOnItsFinalStep()
        {
            tween.Advance(0.7f);
            tween.Advance(0.7f);

            Assert.AreEqual(1f, appliedProgress);
        }
    }
}

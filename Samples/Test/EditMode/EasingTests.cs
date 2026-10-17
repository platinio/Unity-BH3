using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class EasingTests
    {
        private const float Tolerance = 1e-5f;

        private static IEnumerable<string> EveryEasingName() =>
            typeof(Easing).GetMethods(BindingFlags.Public | BindingFlags.Static).Select(method => method.Name);

        private static Func<float, float> EasingNamed(string easingName) =>
            (Func<float, float>)typeof(Easing).GetMethod(easingName).CreateDelegate(typeof(Func<float, float>));

        [TestCaseSource(nameof(EveryEasingName))]
        public void StartsAtZero(string easingName)
        {
            Assert.AreEqual(0f, EasingNamed(easingName)(0f), Tolerance);
        }

        [TestCaseSource(nameof(EveryEasingName))]
        public void EndsAtOne(string easingName)
        {
            Assert.AreEqual(1f, EasingNamed(easingName)(1f), Tolerance);
        }
    }
}

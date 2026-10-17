using ArcaneOnyx.BehaviorTree;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class PunchScaleTests
    {
        private const float HalfOfTheDefaultDuration = 0.125f;

        private GameObject agent;
        private PunchScale punchScale;

        [SetUp]
        public void SetUp()
        {
            agent = new GameObject("Agent");
            BehaviorTreeMachine machine = agent.AddComponent<BehaviorTreeMachine>();

            var graph = new BehaviorTreeGraph();
            graph.SetMachine(machine);

            punchScale = new PunchScale { Position = new Rect(0f, 0f, 150f, 100f) };
            graph.Nodes.Add(punchScale);
            punchScale.SetMachine(machine);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(agent);
            TweenRunnerCleanup.DestroyEveryRunner();
        }

        [Test]
        public void RestoresTheOriginalScaleWhenAbortedHalfway()
        {
            Vector3 originalScale = agent.transform.localScale;

            punchScale.OnNodeEnter();
            TweenRunner.Advance(HalfOfTheDefaultDuration);
            Assume.That(agent.transform.localScale, Is.Not.EqualTo(originalScale));

            punchScale.OnNodeExit();

            Assert.AreEqual(originalScale, agent.transform.localScale);
        }
    }
}

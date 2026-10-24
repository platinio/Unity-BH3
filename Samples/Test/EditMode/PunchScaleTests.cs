using ArcaneOnyx.BehaviorTree;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BH3.Samples.Tests
{
    public class PunchScaleTests
    {
        private const float HalfOfTheDefaultDuration = 0.125f;

        private GameObject agent;
        private Transform body;
        private PunchScale punchScale;

        [SetUp]
        public void SetUp()
        {
            agent = new GameObject("Agent");
            body = new GameObject("Body").transform;
            body.SetParent(agent.transform);
            PointTheUnitAtTheBody(agent.AddComponent<Unit>());
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
            Vector3 originalScale = body.localScale;

            punchScale.OnNodeEnter();
            TweenRunner.Advance(HalfOfTheDefaultDuration);
            Assume.That(body.localScale, Is.Not.EqualTo(originalScale));

            punchScale.OnNodeExit();

            Assert.AreEqual(originalScale, body.localScale);
        }

        private void PointTheUnitAtTheBody(Unit unit)
        {
            var fields = new SerializedObject(unit);
            fields.FindProperty("body").objectReferenceValue = body;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

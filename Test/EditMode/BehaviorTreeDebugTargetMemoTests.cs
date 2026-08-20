using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The shared resolver's memo: what it is allowed to reuse, and what it must never reuse.
    ///
    /// <para>
    /// Driven through the hierarchy-selection route, which is the one branch of <c>Resolve</c> that does not
    /// need play mode — a machine holding a recorder answers from a selection whether or not anything is
    /// running. That is enough to exercise every invalidation rule, because the memo does not care which
    /// branch produced the answer it is holding.
    /// </para>
    ///
    /// <para>
    /// The scene walk itself is not measured here. Whether <c>FindObjectsByType</c> ran is not observable
    /// without a seam that would exist only for the test, so what these assert instead is the half that can
    /// be wrong in a way a user would notice: that a reused answer is still a correct one.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeDebugTargetMemoTests
    {
        private GameObject agent;
        private Object previousSelection;

        [SetUp]
        public void SetUp()
        {
            previousSelection = Selection.activeObject;

            BehaviorTreeDebugTarget.Forget();

            agent = new GameObject("Agent");
        }

        [TearDown]
        public void TearDown()
        {
            if (agent != null) Object.DestroyImmediate(agent);

            Selection.activeObject = previousSelection;
            BehaviorTreeDebugTarget.Forget();
        }

        private BehaviorTreeMachine RecordingMachineSelected()
        {
            var machine = agent.AddComponent<BehaviorTreeMachine>();

            machine.SetFlightRecorder(new BehaviorTreeFlightRecorder("Agent", "Tree"));
            Selection.activeGameObject = agent;

            return machine;
        }

        [Test]
        public void TheSelectedRecordingAgentIsTheAnswer()
        {
            var machine = RecordingMachineSelected();

            Assert.AreSame(machine, BehaviorTreeDebugTarget.Resolve(null, out var source),
                "Fixture check: without this the rest would be asserting that nothing is cached.");
            Assert.AreEqual("selected in the hierarchy", source);
        }

        [Test]
        public void AskingTwiceGivesTheSameAnswerAndTheSameReason()
        {
            var machine = RecordingMachineSelected();

            var first = BehaviorTreeDebugTarget.Resolve(null, out var firstSource);
            var second = BehaviorTreeDebugTarget.Resolve(null, out var secondSource);

            Assert.AreSame(first, second);
            Assert.AreEqual(firstSource, secondSource,
                "The panels ask four to eight times per repaint, and two of them disagreeing within one "
                + "repaint is the failure this class was written to prevent.");
            Assert.AreSame(machine, second);
        }

        /// <summary>
        /// The reason the memo cannot be a plain "same inputs, same answer" cache. A destroyed machine is
        /// still a live managed reference, so an unchecked memo would keep handing it to panels that read
        /// <c>machine.name</c> inside <c>OnGUI</c>.
        /// </summary>
        [Test]
        public void ADestroyedAgentIsNotServedFromTheMemo()
        {
            RecordingMachineSelected();

            Assert.IsNotNull(BehaviorTreeDebugTarget.Resolve(null, out _), "Fixture check.");

            Object.DestroyImmediate(agent);
            agent = null;

            Assert.IsNull(BehaviorTreeDebugTarget.Resolve(null, out _),
                "Within the memo window, and that is exactly when it matters: play mode exits over several "
                + "frames.");
        }

        [Test]
        public void AnAgentThatHasStoppedRecordingIsNotServedFromTheMemo()
        {
            var machine = RecordingMachineSelected();

            Assert.AreSame(machine, BehaviorTreeDebugTarget.Resolve(null, out _), "Fixture check.");

            machine.SetFlightRecorder(null);

            // The clock would catch this within a quarter of a second, which is still a quarter of a second
            // of a LIVE banner over an agent that has stopped recording.
            Assert.IsNull(BehaviorTreeDebugTarget.Resolve(null, out _),
                "Detaching a recorder is the other way an answer stops being one, and it changes none of the "
                + "cheap keys the memo compares.");
        }

        [Test]
        public void ChangingTheSelectionIsNotWaitedOut()
        {
            var machine = RecordingMachineSelected();

            Assert.AreSame(machine, BehaviorTreeDebugTarget.Resolve(null, out _), "Fixture check.");

            var bystander = new GameObject("Bystander");

            try
            {
                Selection.activeGameObject = bystander;

                Assert.IsNull(BehaviorTreeDebugTarget.Resolve(null, out _),
                    "Selection is a user action, and a user action must never be a quarter of a second late.");
            }
            finally
            {
                Object.DestroyImmediate(bystander);
            }
        }
    }
}

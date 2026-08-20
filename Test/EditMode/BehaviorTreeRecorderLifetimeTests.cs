using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Who counts as an agent the debugger can read a history from, and what detaching a recorder actually
    /// takes away.
    ///
    /// <para>
    /// The two are one subject. Every editor-side "is this agent recording?" test reads
    /// <c>machine.FlightRecorder</c>, so whether that property is cleared decides what those tests see, and
    /// whether the machine reference is compared Unity's way decides whether a destroyed agent can answer at
    /// all.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeRecorderLifetimeTests
    {
        private GameObject agent;

        [SetUp]
        public void SetUp()
        {
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;

            agent = new GameObject("Agent");
        }

        [TearDown]
        public void TearDown()
        {
            if (agent != null) Object.DestroyImmediate(agent);

            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        [Test]
        public void DetachingTakesTheRecorderOffTheMachineAndOutOfTheRegistry()
        {
            var machine = agent.AddComponent<BehaviorTreeMachine>();

            BehaviorTreeRecorder.Attach(machine, "Tree");

            Assert.IsNotNull(machine.FlightRecorder, "Fixture check: nothing to detach otherwise.");
            Assert.AreEqual(1, BehaviorTreeFlightRecorders.Active.Count);

            BehaviorTreeRecorder.Detach(machine);

            Assert.IsEmpty(BehaviorTreeFlightRecorders.Active);
            Assert.IsNull(machine.FlightRecorder,
                "Unregistering alone left the recorder reachable through the machine, which is exactly what "
                + "every editor-side recording check reads.");
        }

        [Test]
        public void ADetachedMachineIsNoLongerARecordingTarget()
        {
            var machine = agent.AddComponent<BehaviorTreeMachine>();

            BehaviorTreeRecorder.Attach(machine, "Tree");
            Assert.IsTrue(BehaviorTreeDebugTarget.IsRecording(machine), "Fixture check.");

            BehaviorTreeRecorder.Detach(machine);

            Assert.IsFalse(BehaviorTreeDebugTarget.IsRecording(machine));
        }

        /// <summary>
        /// The state is built deliberately, and saying so matters: with <c>Detach</c> clearing the property,
        /// the route the finding describes -- a machine destroyed on the way out of play mode, still holding
        /// its recorder -- no longer arises on its own. This asserts the rule rather than the route, because
        /// the rule is what the next caller will rely on.
        /// </summary>
        [Test]
        public void ADestroyedMachineIsNotARecordingTargetEvenHoldingARecorder()
        {
            var machine = agent.AddComponent<BehaviorTreeMachine>();

            Object.DestroyImmediate(agent);
            agent = null;

            machine.SetFlightRecorder(new BehaviorTreeFlightRecorder("Agent", "Tree"));

            Assert.IsNotNull((object)machine,
                "The trap: the C# reference is alive, so `is` and `?.` both sail straight past it.");
            Assert.IsTrue(machine is BehaviorTreeMachine, "Which is why the `is` pattern match matched.");
            Assert.IsNotNull(machine.FlightRecorder, "And the recorder is still hanging off it.");

            Assert.IsFalse(BehaviorTreeDebugTarget.IsRecording(machine),
                "A destroyed machine handed to a panel throws MissingReferenceException out of OnGUI when it "
                + "reads machine.name, or draws a dead recording under a LIVE banner.");
        }

        [Test]
        public void AMachineWithNoRecorderIsNotARecordingTarget()
        {
            Assert.IsFalse(BehaviorTreeDebugTarget.IsRecording(agent.AddComponent<BehaviorTreeMachine>()));
        }

        [Test]
        public void ALiveMachineHoldingARecorderIsARecordingTarget()
        {
            var machine = agent.AddComponent<BehaviorTreeMachine>();

            BehaviorTreeRecorder.Attach(machine, "Tree");

            Assert.IsTrue(BehaviorTreeDebugTarget.IsRecording(machine),
                "Otherwise the fix would be to reject everything, which no other test here would catch.");
        }

        [Test]
        public void NothingIsNotARecordingTarget()
        {
            Assert.IsFalse(BehaviorTreeDebugTarget.IsRecording(null));
        }
    }
}

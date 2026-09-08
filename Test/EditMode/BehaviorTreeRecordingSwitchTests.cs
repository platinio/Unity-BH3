using ArcaneOnyx.BehaviorTree.Debugging;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The switch exists to outlive a domain reload, which no edit-mode test can perform. What can be
    /// checked is the contract that makes surviving one possible: the choice is stored, and
    /// <see cref="BehaviorTreeRecordingSwitch.Restore"/> — what the reload runs — puts it back onto the
    /// runtime.
    /// </summary>
    [TestFixture]
    public class BehaviorTreeRecordingSwitchTests
    {
        private bool wasEnabled;

        [SetUp]
        public void SetUp()
        {
            wasEnabled = BehaviorTreeRecordingSwitch.Enabled;
        }

        [TearDown]
        public void TearDown()
        {
            BehaviorTreeRecordingSwitch.Enabled = wasEnabled;
        }

        [Test]
        public void TurningOff_StopsEveryRecorder()
        {
            var recorder = new BehaviorTreeFlightRecorder("Agent", "Tree");

            BehaviorTreeRecordingSwitch.Enabled = false;
            Assert.That(recorder.IsRecording, Is.False, "off should reach a recorder that was never touched");

            BehaviorTreeRecordingSwitch.Enabled = true;
            Assert.That(recorder.IsRecording, Is.True);
        }

        [Test]
        public void Restore_ReappliesOffAfterTheRuntimeForgotIt()
        {
            BehaviorTreeRecordingSwitch.Enabled = false;

            // What a domain reload does to the static: back to its initializer.
            BehaviorTreeFlightRecorders.GloballyEnabled = true;

            BehaviorTreeRecordingSwitch.Restore();

            Assert.That(BehaviorTreeFlightRecorders.GloballyEnabled, Is.False);
        }

        [Test]
        public void Restore_ReappliesOnAfterTheRuntimeWasTurnedOffDirectly()
        {
            BehaviorTreeRecordingSwitch.Enabled = true;
            BehaviorTreeFlightRecorders.GloballyEnabled = false;

            BehaviorTreeRecordingSwitch.Restore();

            Assert.That(BehaviorTreeFlightRecorders.GloballyEnabled, Is.True);
        }

        [Test]
        public void Restore_WithNoChoiceStored_DefaultsToRecording()
        {
            BehaviorTreeRecordingSwitch.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = false;

            BehaviorTreeRecordingSwitch.Restore();

            Assert.That(BehaviorTreeFlightRecorders.GloballyEnabled, Is.True);
        }

        [Test]
        public void Silences_ALiveRecorderOrNoRecording_OnlyWhileOff()
        {
            var recorder = new BehaviorTreeFlightRecorder("Agent", "Tree");

            BehaviorTreeRecordingSwitch.Enabled = true;
            Assert.That(BehaviorTreeRecordingSwitch.Silences(recorder), Is.False);
            Assert.That(BehaviorTreeRecordingSwitch.Silences(null), Is.False);

            BehaviorTreeRecordingSwitch.Enabled = false;
            Assert.That(BehaviorTreeRecordingSwitch.Silences(recorder), Is.True);
            Assert.That(BehaviorTreeRecordingSwitch.Silences(null), Is.True, "nothing resolved yet is still the switch's doing");
        }

        [Test]
        public void Silences_NeverALoadedFile()
        {
            var recorder = new BehaviorTreeFlightRecorder("Agent", "Tree");
            Assert.That(BehaviorTreeRecordingImport.TryFromJson(BehaviorTreeRecordingDump.ToJson(recorder), out var loaded));

            BehaviorTreeRecordingSwitch.Enabled = false;

            Assert.That(BehaviorTreeRecordingSwitch.Silences(loaded), Is.False, "a file was never going to grow");
        }

        [Test]
        public void Reset_ReturnsToRecording()
        {
            BehaviorTreeRecordingSwitch.Enabled = false;

            BehaviorTreeRecordingSwitch.Reset();

            Assert.That(BehaviorTreeRecordingSwitch.Enabled, Is.True);
            Assert.That(BehaviorTreeFlightRecorders.GloballyEnabled, Is.True);
        }
    }
}

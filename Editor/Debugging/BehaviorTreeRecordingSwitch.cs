using ArcaneOnyx.BehaviorTree.Debugging;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// The editor's hand on <see cref="BehaviorTreeFlightRecorders.GloballyEnabled"/>: the same switch, made
    /// to survive a domain reload.
    ///
    /// <para>
    /// The runtime switch is a static, and a static goes back to its default on every domain reload — which
    /// is what happens on the way into play mode. A button that only wrote the static would hold until Play
    /// was pressed and then quietly turn recording back on for the very session it was switched off for.
    /// The choice is kept in <see cref="SessionState"/> and reapplied on load, so the button and the recorders
    /// agree on both sides of the reload.
    /// </para>
    ///
    /// <para>
    /// Session state rather than a preference, because recording defaults to on and everything that reads
    /// the recorder assumes it. A preference carrying "off" into tomorrow's session would put an empty
    /// timeline under someone who never touched the switch. One click per editor session keeps the default
    /// honest.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class BehaviorTreeRecordingSwitch
    {
        private const string Key = "ArcaneOnyx.BH3.RecordingSwitch";

        static BehaviorTreeRecordingSwitch()
        {
            Restore();
        }

        /// <summary>Whether the live flight recorder is on, for every agent at once.</summary>
        public static bool Enabled
        {
            get => BehaviorTreeFlightRecorders.GloballyEnabled;
            set
            {
                BehaviorTreeFlightRecorders.GloballyEnabled = value;
                SessionState.SetBool(Key, value);
            }
        }

        /// <summary>
        /// Puts the session's choice back onto the runtime switch. Runs on every domain reload; public so a
        /// test can stand in for one.
        /// </summary>
        public static void Restore()
        {
            BehaviorTreeFlightRecorders.GloballyEnabled = SessionState.GetBool(Key, true);
        }

        /// <summary>Forgets the session's choice and returns to the default: recording.</summary>
        public static void Reset()
        {
            SessionState.EraseBool(Key);
            Restore();
        }

        /// <summary>
        /// Whether the switch being off is why <paramref name="recording"/> has nothing new to say. A loaded
        /// file was never going to grow, so the switch is not its problem.
        /// </summary>
        public static bool Silences(IBehaviorTreeRecording recording)
        {
            return !Enabled && recording is not BehaviorTreeRecordingSnapshot;
        }

        /// <summary>
        /// What every debugging panel says when the switch is off, so they all point at the same button.
        /// <paramref name="consequence"/> is what that panel in particular stops doing.
        /// </summary>
        public static string OffWarning(string consequence)
        {
            return $"Recording is off, so {consequence}. Turn it back on with the Timeline's Rec button.";
        }
    }
}

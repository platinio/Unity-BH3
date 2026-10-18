using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Every recorder currently running, so a tool can ask what all two hundred agents are doing without
    /// walking the scene for machines.
    /// </summary>
    public static class BehaviorTreeFlightRecorderRegistry
    {
        private static readonly List<BehaviorTreeFlightRecorder> activeRecorders = new();

        public static bool IsGloballyEnabled { get; set; } = true;

        /// <summary>
        /// Separate from <see cref="IsGloballyEnabled"/> because the two cost very different amounts: an
        /// event is a struct written into a pre-allocated slot, a guard trace walks a script graph.
        /// </summary>
        public static bool IsTracingGloballyEnabled { get; set; } = true;

        /// <summary>
        /// Handed to recorders created from here on. Lowering it does not shrink existing ones, because a
        /// ring is allocated once and never resized.
        /// </summary>
        public static int DefaultCapacity { get; set; } = BehaviorTreeFlightRecorder.DefaultCapacity;

        public static int DefaultTraceCapacity { get; set; } = GuardTraceRing.DefaultCapacity;

        public static IReadOnlyList<BehaviorTreeFlightRecorder> Active => activeRecorders;

        public static void Register(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null || activeRecorders.Contains(recorder)) return;

            activeRecorders.Add(recorder);
        }

        public static void Unregister(BehaviorTreeFlightRecorder recorder)
        {
            if (recorder == null) return;

            activeRecorders.Remove(recorder);
            BehaviorTreeBreakpoints.Forget(recorder);
        }

        public static void Reset()
        {
            foreach (var recorder in activeRecorders)
            {
                BehaviorTreeBreakpoints.Forget(recorder);
            }

            activeRecorders.Clear();
        }

        public static void ClearAll()
        {
            foreach (var recorder in activeRecorders)
            {
                recorder.Clear();
            }
        }

        /// <summary>
        /// With domain reload disabled this list is not re-created on the way into play mode, so whatever it
        /// held is what the new session would start with. The switches are deliberately left alone:
        /// the editor's recording switch restores them on a domain reload and relies on them persisting
        /// without one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForPlaySession()
        {
            Reset();
        }
    }
}

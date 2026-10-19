using System.Collections.Generic;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public static class BehaviorTreeFlightRecorderRegistry
    {
        private static readonly List<BehaviorTreeFlightRecorder> activeRecorders = new();

        public static bool IsGloballyEnabled { get; set; } = true;

        public static bool IsTracingGloballyEnabled { get; set; } = true;

        /// <summary>Lowering this does not shrink recorders that already exist; a ring is never resized.</summary>
        public static int DefaultEventCapacity { get; set; } = BehaviorTreeFlightRecorder.DefaultEventCapacity;

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
        /// With domain reload disabled this list is not re-created on the way into play mode, so whatever
        /// it held is what the new session would start with. The switches are left alone on purpose.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForPlaySession()
        {
            Reset();
        }
    }
}

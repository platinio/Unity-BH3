using System;
using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Every armed breakpoint, and the one place they are matched against what the tree just did.
    ///
    /// <para>
    /// <b>One choke point, not four.</b> The recorder already funnels every event it keeps through
    /// <c>BehaviorTreeFlightRecorder.Add</c>, and by the time an event reaches there it carries everything a
    /// breakpoint needs to match on — kind, call site, the subject's guid, a guard's new answer, a variable's
    /// name and its new value. So this hooks that single call rather than threading checks back through the
    /// node lifecycle, and <see cref="BehaviorTreeNode"/> is untouched by this component. It also means guard
    /// breakpoints inherit the recorder's transition filtering for free: <c>GuardEval</c> is only reached when
    /// the answer actually changed, which is the difference between a usable breakpoint and one that pauses
    /// the editor on every tick a guard is false.
    /// </para>
    ///
    /// <para>
    /// <b>The consequence, stated plainly: breakpoints fire only while the agent is recording.</b> Turning a
    /// recorder off, or <see cref="BehaviorTreeFlightRecorders.GloballyEnabled"/> off, turns breakpoints off
    /// with it. That is the right trade rather than an accident — everything the editor does on a hit
    /// (park the scrubber on the tick, explain the moment, name the writer) reads the recording, so a
    /// breakpoint without one could pause the editor and then have nothing to show. The breakpoints panel says
    /// so on screen rather than leaving it to be discovered.
    /// </para>
    ///
    /// <para>
    /// Cost when nothing is armed is one <c>Count</c> compare per recorded event, which is why the check can
    /// sit on the recorder's hot path at all. The whole class is unreachable outside the editor and dev builds
    /// regardless, since the only caller is behind <see cref="BehaviorTreeRecorder"/>'s
    /// <see cref="System.Diagnostics.ConditionalAttribute"/> gate.
    /// </para>
    /// </summary>
    public static class BehaviorTreeBreakpoints
    {
        private static readonly List<BehaviorTreeBreakpoint> all = new();

        // Indexes into `all`. A node and a guard can share a guid — a ConditionalExecution is a node — so they
        // are separate maps rather than one keyed by guid.
        private static readonly Dictionary<Guid, BehaviorTreeBreakpoint> nodes = new();
        private static readonly Dictionary<Guid, BehaviorTreeBreakpoint> guards = new();
        private static readonly Dictionary<string, BehaviorTreeBreakpoint> variables = new(StringComparer.Ordinal);

        /// <summary>
        /// The kill switch, mirroring <see cref="BehaviorTreeFlightRecorders.GloballyEnabled"/>. Off means
        /// nothing fires while every breakpoint stays armed, which is what you want when you have found the
        /// bug and would like to watch the rest of the run without re-arming six breakpoints afterwards.
        /// </summary>
        public static bool GloballyEnabled { get; set; } = true;

        /// <summary>Every breakpoint, in the order they were armed. The panel's list.</summary>
        public static IReadOnlyList<BehaviorTreeBreakpoint> All => all;

        /// <summary>
        /// Raised when one fires, before the tree carries on. The runtime raises; the editor decides what a
        /// hit means — pausing, selecting, scrubbing — so nothing here needs an editor type and the store
        /// stays testable without a scene.
        ///
        /// <para>
        /// Handlers must not touch the tree. This is raised from inside the recorder, mid-tick, and the
        /// recorder's contract is that it observes and never participates; a handler that wrote a variable or
        /// moved the playhead of the tree itself would change the run it is supposed to be reporting on.
        /// </para>
        /// </summary>
        public static event Action<BehaviorTreeBreakpointHit> Hit;

        #region Arming

        /// <summary>
        /// Arms or edits the breakpoint on a node. Returns the live breakpoint either way, so a caller that
        /// wants to label it does not have to look it up again.
        /// </summary>
        public static BehaviorTreeBreakpoint SetNode(Guid node, BehaviorTreeNodeBreakEvents events)
        {
            if (node == Guid.Empty) return null;

            // An empty mask is a removal rather than a breakpoint that can never fire. The menu toggles one
            // flag at a time, so clearing the last one is how a designer disarms a node, and leaving a dead
            // row in the panel would be the wrong reading of that.
            if (events == BehaviorTreeNodeBreakEvents.None)
            {
                Remove(Find(BehaviorTreeBreakpointKind.Node, node, null));
                return null;
            }

            if (nodes.TryGetValue(node, out var existing))
            {
                existing.Events = events;
                return existing;
            }

            return Add(BehaviorTreeBreakpoint.ForNode(node, events));
        }

        /// <summary>Arms or edits the breakpoint on a guard.</summary>
        public static BehaviorTreeBreakpoint SetGuard(Guid guard, BehaviorTreeGuardBreakOn breakOn)
        {
            if (guard == Guid.Empty) return null;

            if (guards.TryGetValue(guard, out var existing))
            {
                existing.GuardBreakOn = breakOn;
                return existing;
            }

            return Add(BehaviorTreeBreakpoint.ForGuard(guard, breakOn));
        }

        /// <summary>
        /// Arms or edits the breakpoint on a variable. <paramref name="expectedValue"/> null means any write.
        /// </summary>
        public static BehaviorTreeBreakpoint SetVariable(string key, string expectedValue = null)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (variables.TryGetValue(key, out var existing))
            {
                existing.ExpectedValue = string.IsNullOrEmpty(expectedValue) ? null : expectedValue;
                return existing;
            }

            return Add(BehaviorTreeBreakpoint.ForVariable(key, expectedValue));
        }

        /// <summary>
        /// Adds an already-built breakpoint, replacing whatever was watching the same target. Used by the
        /// importer that reads the file back; prefer the <c>Set*</c> methods everywhere else.
        /// </summary>
        public static BehaviorTreeBreakpoint Add(BehaviorTreeBreakpoint breakpoint)
        {
            if (breakpoint == null) return null;

            Remove(Find(breakpoint.Kind, breakpoint.TargetGuid, breakpoint.VariableKey));

            all.Add(breakpoint);
            Index(breakpoint);

            return breakpoint;
        }

        public static void Remove(BehaviorTreeBreakpoint breakpoint)
        {
            if (breakpoint == null || !all.Remove(breakpoint)) return;

            switch (breakpoint.Kind)
            {
                case BehaviorTreeBreakpointKind.Node:
                    nodes.Remove(breakpoint.TargetGuid);
                    break;
                case BehaviorTreeBreakpointKind.Guard:
                    guards.Remove(breakpoint.TargetGuid);
                    break;
                default:
                    if (breakpoint.VariableKey != null) variables.Remove(breakpoint.VariableKey);
                    break;
            }
        }

        public static void Clear()
        {
            all.Clear();
            nodes.Clear();
            guards.Clear();
            variables.Clear();
        }

        /// <summary>Enables or disables one without losing it. The panel's checkbox.</summary>
        public static void SetEnabled(BehaviorTreeBreakpoint breakpoint, bool enabled)
        {
            if (breakpoint != null) breakpoint.Enabled = enabled;
        }

        /// <summary>Labels one for the panel and the log line. Cosmetic; nothing matches on it.</summary>
        public static void Describe(BehaviorTreeBreakpoint breakpoint, string label, string treeName)
        {
            if (breakpoint == null) return;

            breakpoint.Label = label;
            breakpoint.TreeName = treeName;
        }

        /// <summary>
        /// Zeroes the hit counters. Called when play mode starts: a count carried over from the previous run
        /// describes a run that no longer exists, and the panel showing "12 hits" for a session that has not
        /// begun is a lie of exactly the kind this whole feature exists to remove.
        /// </summary>
        public static void ResetHitCounts()
        {
            foreach (var breakpoint in all)
            {
                breakpoint.HitCount = 0;
            }
        }

        #endregion

        #region Lookup

        public static BehaviorTreeBreakpoint ForNode(Guid node) =>
            nodes.TryGetValue(node, out var found) ? found : null;

        public static BehaviorTreeBreakpoint ForGuard(Guid guard) =>
            guards.TryGetValue(guard, out var found) ? found : null;

        public static BehaviorTreeBreakpoint ForVariable(string key) =>
            key != null && variables.TryGetValue(key, out var found) ? found : null;

        /// <summary>
        /// Whether anything is armed on a node or a guard with this guid, for the canvas dot. Cheap enough to
        /// call once per node per repaint, which is what the widget does.
        /// </summary>
        public static bool IsArmedOn(Guid guid) => nodes.ContainsKey(guid) || guards.ContainsKey(guid);

        private static BehaviorTreeBreakpoint Find(BehaviorTreeBreakpointKind kind, Guid target, string key)
        {
            return kind switch
            {
                BehaviorTreeBreakpointKind.Node => ForNode(target),
                BehaviorTreeBreakpointKind.Guard => ForGuard(target),
                _ => ForVariable(key),
            };
        }

        private static void Index(BehaviorTreeBreakpoint breakpoint)
        {
            switch (breakpoint.Kind)
            {
                case BehaviorTreeBreakpointKind.Node:
                    nodes[breakpoint.TargetGuid] = breakpoint;
                    break;
                case BehaviorTreeBreakpointKind.Guard:
                    guards[breakpoint.TargetGuid] = breakpoint;
                    break;
                default:
                    if (breakpoint.VariableKey != null) variables[breakpoint.VariableKey] = breakpoint;
                    break;
            }
        }

        #endregion

        #region Matching

        /// <summary>
        /// Matches one recorded event against the armed breakpoints, raising <see cref="Hit"/> for the one
        /// that fires.
        ///
        /// <para>
        /// <see cref="BehaviorTreeFlightRecorder"/> is the only intended caller. It is public rather than
        /// internal so the tests, which live in their own assembly, can drive a known event through it —
        /// the same reason <see cref="BehaviorTreeFlightRecorder.ExternalVariableWrite"/> is public. Do not
        /// call it from gameplay code: it would report a hit for something the tree did not do.
        /// </para>
        /// </summary>
        public static void Evaluate(IBehaviorTreeRecording recording, in BehaviorTreeEvent recorded)
        {
            // The hot path, and the reason it is safe to check on every event: no breakpoints means one
            // integer compare and a return, before anything is read off the event.
            if (all.Count == 0 || !GloballyEnabled) return;

            var breakpoint = MatchOf(recorded);
            if (breakpoint == null) return;

            breakpoint.HitCount++;

            Hit?.Invoke(new BehaviorTreeBreakpointHit(
                breakpoint, recording, recording?.AgentName, recorded));
        }

        /// <summary>
        /// The breakpoint this event trips, or null. Separated from <see cref="Evaluate"/> so a test can ask
        /// what would fire without a subscriber and without a hit count moving.
        /// </summary>
        public static BehaviorTreeBreakpoint MatchOf(in BehaviorTreeEvent recorded)
        {
            switch (recorded.Kind)
            {
                case BehaviorTreeEventKind.NodeEnter:
                    return NodeMatch(recorded.NodeGuid, BehaviorTreeNodeBreakEvents.Enter);
                case BehaviorTreeEventKind.NodeExit:
                    return NodeMatch(recorded.NodeGuid, BehaviorTreeNodeBreakEvents.Exit);
                case BehaviorTreeEventKind.NodeAborted:
                    return NodeMatch(recorded.NodeGuid, BehaviorTreeNodeBreakEvents.Aborted);
                case BehaviorTreeEventKind.NodeSkipped:
                    return NodeMatch(recorded.NodeGuid, BehaviorTreeNodeBreakEvents.Skipped);

                // NodeGuid is the guard here and RelatedGuid is the node it protects — the one event kind whose
                // subject is not a behaviour node. Reading them the usual way round would arm every breakpoint
                // against the wrong half of the tree.
                case BehaviorTreeEventKind.GuardEval:
                {
                    var guard = ForGuard(recorded.NodeGuid);
                    return guard != null && guard.Enabled && guard.MatchesGuard(recorded.Flag) ? guard : null;
                }

                case BehaviorTreeEventKind.VariableWrite:
                {
                    var variable = ForVariable(recorded.Key);
                    return variable != null && variable.Enabled && variable.MatchesWrite(recorded.NewValue)
                        ? variable
                        : null;
                }

                // TreePushed and TreePopped are the recorder's own bookkeeping around a node that already
                // reported its enter and exit, so breaking on them would fire twice for one thing happening.
                default:
                    return null;
            }
        }

        private static BehaviorTreeBreakpoint NodeMatch(Guid guid, BehaviorTreeNodeBreakEvents moment)
        {
            var breakpoint = ForNode(guid);

            return breakpoint != null && breakpoint.Enabled && breakpoint.Matches(moment) ? breakpoint : null;
        }

        #endregion
    }
}

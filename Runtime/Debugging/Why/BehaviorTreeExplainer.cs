using System;
using System.Collections.Generic;
using System.Text;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Turns a recording into the answer to "why did this node do that".
    ///
    /// <para>
    /// The rule the whole class is built around: every sentence must be justifiable from recorded events. A
    /// behavior tree branch changes for a small closed set of reasons — a guard flipped, a child returned a
    /// status, or a composite fell through — and Component 1 records all three, so the answer can be read off
    /// the buffer instead of reconstructed from the canvas and a hunch. Where the buffer supports only a
    /// correlation, the clause says so rather than dressing it up as a cause.
    /// </para>
    ///
    /// <para>
    /// Pure and static: no Unity types, no editor types, no live machine. That is what lets the same engine
    /// serve the sidebar panel, an exported recording opened a week later, a CLI, and an authoring agent
    /// checking a tree it generated.
    /// </para>
    /// </summary>
    public static class BehaviorTreeExplainer
    {
        /// <summary>
        /// How far back to look for repeated enter/abort cycles. Long enough to catch a guard flickering every
        /// few ticks, short enough that a branch which legitimately restarts now and then is not accused of it.
        /// </summary>
        private const int OscillationWindow = 60;

        /// <summary>Aborts within the window before it is worth mentioning.</summary>
        private const int OscillationThreshold = 3;

        /// <summary>
        /// Explains the node's most recent episode at or before <paramref name="atTick"/>.
        /// </summary>
        /// <param name="atTick">The vantage point. Negative means the end of the recording.</param>
        public static BehaviorTreeExplanation Explain(
            IBehaviorTreeRecording recording,
            int callSiteId,
            Guid nodeGuid,
            IBehaviorTreeTopology topology = null,
            int atTick = -1)
        {
            if (recording == null) throw new ArgumentNullException(nameof(recording));

            if (atTick < 0) atTick = recording.Tick;

            var subject = NameOf(topology, nodeGuid);
            var path = CallSitePath(recording, callSiteId);
            var clauses = new List<BehaviorTreeExplanationClause>();

            var episode = LastLifecycleIndex(recording, callSiteId, nodeGuid, atTick);

            if (episode < 0)
            {
                // No lifecycle events. Either it is a guard (which only ever emits GuardEval), or it genuinely
                // never ran — two very different answers, so check for the first before concluding the second.
                if (LastGuardEvalIndex(recording, callSiteId, nodeGuid, atTick) >= 0)
                {
                    return ExplainGuard(recording, callSiteId, nodeGuid, topology, atTick, subject, path, clauses);
                }

                return ExplainNeverRan(recording, callSiteId, nodeGuid, topology, atTick, subject, path, clauses);
            }

            episode = PreferCauseOverItsOwnExit(recording, callSiteId, nodeGuid, episode);

            var last = recording.EventAt(episode);

            switch (last.Kind)
            {
                case BehaviorTreeEventKind.NodeAborted:
                    return ExplainAborted(recording, callSiteId, nodeGuid, topology, atTick, subject, path, clauses, episode);

                case BehaviorTreeEventKind.NodeSkipped:
                    return ExplainSkipped(recording, callSiteId, nodeGuid, topology, atTick, subject, path, clauses, episode);

                case BehaviorTreeEventKind.NodeTakenOver:
                    return ExplainTakenOver(recording, callSiteId, nodeGuid, topology, atTick, subject, path, clauses, episode);

                case BehaviorTreeEventKind.NodeExit:
                    return ExplainExited(recording, callSiteId, nodeGuid, topology, atTick, subject, path, clauses, episode);

                default:
                    return ExplainRunning(recording, callSiteId, nodeGuid, topology, atTick, subject, path, clauses, episode);
            }
        }

        /// <summary>
        /// Which call sites this guid ran in. The canvas knows the node a designer clicked but not which copy
        /// of a shared branch they meant, and when a branch is used twice the answer differs per copy.
        /// </summary>
        public static IReadOnlyList<int> CallSitesFor(IBehaviorTreeRecording recording, Guid nodeGuid)
        {
            var found = new List<int>();
            if (recording == null || nodeGuid == Guid.Empty) return found;

            for (int i = 0; i < recording.EventCount; i++)
            {
                var recorded = recording.EventAt(i);
                if (recorded.NodeGuid != nodeGuid && recorded.RelatedGuid != nodeGuid) continue;
                if (found.Contains(recorded.CallSiteId)) continue;

                found.Add(recorded.CallSiteId);
            }

            return found;
        }

        #region Outcomes

        private static BehaviorTreeExplanation ExplainAborted(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, string subject, string path, List<BehaviorTreeExplanationClause> clauses, int episode)
        {
            var abort = recording.EventAt(episode);
            var guardGuid = abort.RelatedGuid;
            var guardName = NameOf(topology, guardGuid);

            var headline = guardGuid == Guid.Empty
                ? $"Aborted at tick {abort.Tick} while running."
                : $"Aborted at tick {abort.Tick}: guard '{guardName}' turned false while it was running.";

            if (guardGuid != Guid.Empty)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Cause,
                    $"Guard '{guardName}' aborted it, so everything under it stopped on the same tick.",
                    BehaviorTreeExplanationLink.ToGuard(guardGuid, callSiteId, abort.Tick, abort.Sequence)));
            }

            AddEnteredClause(recording, callSiteId, nodeGuid, clauses, episode, abort.Tick);

            var exit = ExitAfterAbort(recording, callSiteId, nodeGuid, episode);
            if (exit >= 0)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Context,
                    $"It returned {recording.EventAt(exit).Status} to its parent on the same tick.",
                    BehaviorTreeExplanationLink.ToTick(abort.Tick, callSiteId, recording.EventAt(exit).Sequence)));
            }

            GuardTrace trace = null;

            var flip = LastGuardTransitionIndex(recording, callSiteId, guardGuid, episode, false);
            if (flip >= 0)
            {
                var flipped = recording.EventAt(flip);
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Evidence,
                    $"The guard was last recorded turning false at tick {flipped.Tick} (step {flipped.Sequence}).",
                    BehaviorTreeExplanationLink.ToTick(flipped.Tick, callSiteId, flipped.Sequence)));

                trace = AddTraceClause(recording, flip, clauses);

                AddWriteCause(recording, topology, guardGuid, flip, clauses);
            }

            AddOscillationClause(recording, callSiteId, nodeGuid, atTick, clauses);
            AddClippedCaveat(recording, clauses);

            return new BehaviorTreeExplanation(
                nodeGuid, callSiteId, subject, path, atTick, BehaviorTreeOutcome.Aborted, headline, clauses, trace);
        }

        private static BehaviorTreeExplanation ExplainSkipped(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, string subject, string path, List<BehaviorTreeExplanationClause> clauses, int episode)
        {
            var skip = recording.EventAt(episode);
            var guardGuid = skip.RelatedGuid;
            var guardName = NameOf(topology, guardGuid);

            // Deliberately different words from an abort. "Never entered" and "ran and was killed" are
            // different answers to "why didn't this happen", and conflating them was the bug Finding 3 records.
            var headline = guardGuid == Guid.Empty
                ? $"Never entered at tick {skip.Tick}: a guard was false."
                : $"Never entered at tick {skip.Tick}: guard '{guardName}' was false when it was about to start.";

            GuardTrace trace = null;

            if (guardGuid != Guid.Empty)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Cause,
                    $"Guard '{guardName}' was false, so the node was skipped rather than interrupted — it never started.",
                    BehaviorTreeExplanationLink.ToGuard(guardGuid, callSiteId, skip.Tick, skip.Sequence)));

                var flip = LastGuardTransitionIndex(recording, callSiteId, guardGuid, episode, false);
                if (flip >= 0)
                {
                    var flipped = recording.EventAt(flip);
                    var since = flipped.Tick == skip.Tick
                        ? $"The guard turned false on this same tick (step {flipped.Sequence})."
                        : $"The guard has been false since tick {flipped.Tick}.";

                    clauses.Add(new BehaviorTreeExplanationClause(
                        BehaviorTreeClauseRole.Evidence, since,
                        BehaviorTreeExplanationLink.ToTick(flipped.Tick, callSiteId, flipped.Sequence)));

                    trace = AddTraceClause(recording, flip, clauses);

                    AddWriteCause(recording, topology, guardGuid, flip, clauses);
                }
            }

            AddSiblingClause(recording, callSiteId, nodeGuid, topology, skip.Tick, clauses);
            AddClippedCaveat(recording, clauses);

            return new BehaviorTreeExplanation(
                nodeGuid, callSiteId, subject, path, atTick, BehaviorTreeOutcome.Skipped, headline, clauses, trace);
        }

        /// <summary>
        /// A running branch that gave way to a higher-priority sibling.
        ///
        /// <para>
        /// Deliberately its own words, not an abort's. Nothing under this node turned false — something that
        /// outranks it became able to run — so the place to look next is the preemptor's guard and the write
        /// that woke it, not this branch at all. The recorder keeps the two apart for exactly this sentence.
        /// </para>
        /// </summary>
        private static BehaviorTreeExplanation ExplainTakenOver(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, string subject, string path, List<BehaviorTreeExplanationClause> clauses, int episode)
        {
            var takeover = recording.EventAt(episode);
            var guardGuid = takeover.RelatedGuid;
            var guardName = NameOf(topology, guardGuid);

            // The event carries the preemptor twice: its guid in NewValue for the topology and the canvas,
            // and its name at the moment of recording in Key — which is what an imported recording with no
            // tree to ask still has.
            var preemptorGuid = Guid.TryParse(takeover.NewValue, out var parsed) ? parsed : Guid.Empty;
            var preemptorName = PreemptorName(topology, preemptorGuid, takeover.Key);

            var headline = guardGuid == Guid.Empty
                ? $"Taken over at tick {takeover.Tick}: '{preemptorName}' took its slot."
                : $"Taken over at tick {takeover.Tick}: '{preemptorName}' took its slot when guard '{guardName}' allowed it.";

            clauses.Add(new BehaviorTreeExplanationClause(
                BehaviorTreeClauseRole.Cause,
                $"'{preemptorName}' outranks it and became able to run, so this branch was stopped to make way "
                + "— nothing under this node turned false.",
                preemptorGuid == Guid.Empty
                    ? BehaviorTreeExplanationLink.ToTick(takeover.Tick, callSiteId, takeover.Sequence)
                    : BehaviorTreeExplanationLink.ToNode(preemptorGuid, callSiteId, takeover.Tick, takeover.Sequence)));

            AddEnteredClause(recording, callSiteId, nodeGuid, clauses, episode, takeover.Tick);

            var exit = ExitAfterAbort(recording, callSiteId, nodeGuid, episode);
            if (exit >= 0)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Context,
                    $"It returned {recording.EventAt(exit).Status} to its parent on the same tick.",
                    BehaviorTreeExplanationLink.ToTick(takeover.Tick, callSiteId, recording.EventAt(exit).Sequence)));
            }

            GuardTrace trace = null;

            // True, not false: the preemptor's guard opening is what evicted this branch. Everything an abort
            // hangs off the fall to false hangs here off the rise to true — the trace of what the guard read,
            // and the write that changed it.
            var flip = LastGuardTransitionIndex(recording, callSiteId, guardGuid, episode, true);
            if (flip >= 0)
            {
                var flipped = recording.EventAt(flip);
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Evidence,
                    $"Guard '{guardName}' was last recorded turning true at tick {flipped.Tick} (step {flipped.Sequence}).",
                    BehaviorTreeExplanationLink.ToTick(flipped.Tick, callSiteId, flipped.Sequence)));

                trace = AddTraceClause(recording, flip, clauses);

                AddWriteCause(recording, topology, guardGuid, flip, clauses);
            }

            AddOscillationClause(recording, callSiteId, nodeGuid, atTick, clauses);
            AddClippedCaveat(recording, clauses);

            return new BehaviorTreeExplanation(
                nodeGuid, callSiteId, subject, path, atTick, BehaviorTreeOutcome.TakenOver, headline, clauses, trace);
        }

        /// <summary>
        /// What to call the branch that took the slot: the topology's current name when it has one, the name
        /// recorded at the moment of the takeover when it does not, and an honest generic when neither exists.
        /// </summary>
        private static string PreemptorName(IBehaviorTreeTopology topology, Guid preemptorGuid, string recordedName)
        {
            if (topology != null && preemptorGuid != Guid.Empty &&
                topology.TryGetNode(preemptorGuid, out var info) && !string.IsNullOrEmpty(info.DisplayName))
            {
                return info.DisplayName;
            }

            return string.IsNullOrEmpty(recordedName) ? "a higher-priority branch" : recordedName;
        }

        private static BehaviorTreeExplanation ExplainExited(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, string subject, string path, List<BehaviorTreeExplanationClause> clauses, int episode)
        {
            var exit = recording.EventAt(episode);
            var outcome = exit.Status == ExecutionStatus.Success
                ? BehaviorTreeOutcome.Succeeded
                : BehaviorTreeOutcome.Failed;

            var headline = $"Exited {exit.Status} at tick {exit.Tick}.";

            AddEnteredClause(recording, callSiteId, nodeGuid, clauses, episode, exit.Tick);

            // The child whose status the parent passed on. Only claimed when the topology confirms the
            // parentage — "the node that exited just before this one" is a guess, and a guess in a tool built
            // to replace guessing is worse than saying nothing.
            var child = LastChildExitIndex(recording, callSiteId, nodeGuid, topology, episode, exit.Tick);
            if (child >= 0)
            {
                var childExit = recording.EventAt(child);
                var childName = NameOf(topology, childExit.NodeGuid);

                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Cause,
                    $"Its child '{childName}' returned {childExit.Status} on the same tick (step {childExit.Sequence}).",
                    BehaviorTreeExplanationLink.ToNode(childExit.NodeGuid, callSiteId, childExit.Tick, childExit.Sequence)));
            }

            AddOscillationClause(recording, callSiteId, nodeGuid, atTick, clauses);
            AddClippedCaveat(recording, clauses);

            return new BehaviorTreeExplanation(nodeGuid, callSiteId, subject, path, atTick, outcome, headline, clauses);
        }

        private static BehaviorTreeExplanation ExplainRunning(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, string subject, string path, List<BehaviorTreeExplanationClause> clauses, int episode)
        {
            var enter = recording.EventAt(episode);
            var ticks = atTick - enter.Tick + 1;

            var headline = $"Running since tick {enter.Tick} ({ticks} {(ticks == 1 ? "tick" : "ticks")}).";

            clauses.Add(new BehaviorTreeExplanationClause(
                BehaviorTreeClauseRole.Evidence,
                $"Entered at tick {enter.Tick} and has not exited.",
                BehaviorTreeExplanationLink.ToTick(enter.Tick, callSiteId, enter.Sequence)));

            AddActiveGuardsClause(recording, callSiteId, nodeGuid, topology, atTick, clauses);
            AddOscillationClause(recording, callSiteId, nodeGuid, atTick, clauses);
            AddClippedCaveat(recording, clauses);

            return new BehaviorTreeExplanation(
                nodeGuid, callSiteId, subject, path, atTick, BehaviorTreeOutcome.Running, headline, clauses);
        }

        private static BehaviorTreeExplanation ExplainGuard(
            IBehaviorTreeRecording recording, int callSiteId, Guid guardGuid, IBehaviorTreeTopology topology,
            int atTick, string subject, string path, List<BehaviorTreeExplanationClause> clauses)
        {
            var index = LastGuardEvalIndex(recording, callSiteId, guardGuid, atTick);
            var eval = recording.EventAt(index);
            var ownerName = NameOf(topology, eval.RelatedGuid);

            var headline = $"Guard is {(eval.Flag ? "true" : "false")}, since tick {eval.Tick}.";

            if (eval.RelatedGuid != Guid.Empty)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Context,
                    $"It guards '{ownerName}', and is re-evaluated every tick while that node runs.",
                    BehaviorTreeExplanationLink.ToNode(eval.RelatedGuid, callSiteId)));
            }

            var trace = AddTraceClause(recording, index, clauses);

            AddWriteCause(recording, topology, guardGuid, index, clauses);

            var flips = CountGuardTransitions(recording, callSiteId, guardGuid, atTick - OscillationWindow, atTick);
            if (flips >= OscillationThreshold)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Context,
                    $"It changed value {flips} times in the last {OscillationWindow} ticks — a flickering guard is the usual cause of a branch that keeps restarting."));
            }

            AddClippedCaveat(recording, clauses);

            return new BehaviorTreeExplanation(
                guardGuid, callSiteId, subject, path, atTick,
                eval.Flag ? BehaviorTreeOutcome.Running : BehaviorTreeOutcome.Skipped, headline, clauses, trace);
        }

        private static BehaviorTreeExplanation ExplainNeverRan(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, string subject, string path, List<BehaviorTreeExplanationClause> clauses)
        {
            // "Nothing about this node is in the recording" is only true when the vantage point is the end of
            // it. Explaining at a scrubbed tick, the node may simply not have started yet — and saying "never"
            // there is a claim about the whole recording that the recording itself contradicts a few ticks
            // later. It also wastes the most useful thing this answer could carry: where to scrub to.
            var upcoming = FirstLifecycleIndexAfter(recording, callSiteId, nodeGuid, atTick);

            var headline = upcoming >= 0
                ? $"Has not run yet as of tick {atTick}: the first thing recorded about it is at tick {recording.EventAt(upcoming).Tick}."
                : "Never ran: nothing about this node is in the recording.";

            if (upcoming >= 0)
            {
                var first = recording.EventAt(upcoming);

                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Context,
                    first.Kind == BehaviorTreeEventKind.NodeEnter
                        ? $"It enters at tick {first.Tick}."
                        : $"The first thing recorded about it is {first.Kind} at tick {first.Tick}.",
                    BehaviorTreeExplanationLink.ToTick(first.Tick, callSiteId, first.Sequence)));
            }

            AddSiblingClause(recording, callSiteId, nodeGuid, topology, atTick, clauses);

            // Without this the sentence overclaims. An empty buffer and a node that never ran look identical
            // from here, and only one of them is the node's fault.
            if (recording.Dropped > 0)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Caveat,
                    $"The recording is clipped — {recording.Dropped} events were dropped off the back — so this node may have run before the buffer's start."));
            }
            else if (recording.EventCount == 0)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Caveat,
                    "The recording is empty. Check that recording is enabled for this agent."));
            }

            return new BehaviorTreeExplanation(
                nodeGuid, callSiteId, subject, path, atTick, BehaviorTreeOutcome.NoRecord, headline, clauses);
        }

        #endregion

        #region Shared clauses

        private static void AddEnteredClause(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid,
            List<BehaviorTreeExplanationClause> clauses, int before, int endTick)
        {
            var enter = LastIndexOf(recording, callSiteId, nodeGuid, BehaviorTreeEventKind.NodeEnter, before);
            if (enter < 0) return;

            var entered = recording.EventAt(enter);
            var ticks = endTick - entered.Tick + 1;

            clauses.Add(new BehaviorTreeExplanationClause(
                BehaviorTreeClauseRole.Context,
                $"It entered at tick {entered.Tick} and ran for {ticks} {(ticks == 1 ? "tick" : "ticks")}.",
                BehaviorTreeExplanationLink.ToTick(entered.Tick, callSiteId, entered.Sequence)));
        }

        /// <summary>
        /// What changed just before a guard's result did.
        ///
        /// <para>
        /// When the topology can say which variables the guard reads this is a cause and is worded as one.
        /// When it cannot — a guard fed by a script graph, or no topology supplied — the most recent write is
        /// still the most useful thing to show, but it is a correlation and gets a caveat saying so. The
        /// difference matters: a designer who trusts a coincidence wastes an afternoon on the wrong variable.
        /// </para>
        /// </summary>
        private static void AddWriteCause(
            IBehaviorTreeRecording recording, IBehaviorTreeTopology topology, Guid guardGuid, int flipIndex,
            List<BehaviorTreeExplanationClause> clauses)
        {
            IReadOnlyList<string> keys = null;
            var resolved = topology != null && guardGuid != Guid.Empty &&
                           topology.TryGetGuardReads(guardGuid, out keys) && keys != null && keys.Count > 0;

            var write = LastWriteIndex(recording, flipIndex, resolved ? keys : null);
            if (write < 0)
            {
                if (resolved)
                {
                    clauses.Add(new BehaviorTreeExplanationClause(
                        BehaviorTreeClauseRole.Caveat,
                        $"No recorded write to {Join(keys)} precedes the change. The value may be written from inside a Visual Scripting graph, which the recorder cannot see."));
                }

                return;
            }

            var written = recording.EventAt(write);
            var writer = WriterName(written, topology);
            var text = $"'{written.Key}' changed {written.OldValue} -> {written.NewValue} at tick {written.Tick} (step {written.Sequence}), written by {writer}.";

            clauses.Add(new BehaviorTreeExplanationClause(
                resolved ? BehaviorTreeClauseRole.Cause : BehaviorTreeClauseRole.Evidence,
                text,
                BehaviorTreeExplanationLink.ToVariable(written.Key, written.CallSiteId, written.Tick, written.Sequence)));

            if (!resolved)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Caveat,
                    "That is the last recorded write before the change, not a proven cause — the guard's inputs could not be resolved statically."));
            }
            else if (written.RelatedGuid != Guid.Empty)
            {
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Evidence,
                    $"Written by node '{writer}'.",
                    BehaviorTreeExplanationLink.ToNode(written.RelatedGuid, written.CallSiteId, written.Tick, written.Sequence)));
            }
        }

        /// <summary>
        /// The chain the guard was reading, when a trace was captured for this flip.
        ///
        /// <para>
        /// This is the clause that answers the question "it returned false" only restates. Most branches in a
        /// real tree are guarded by a Visual Scripting graph, so without the trace the account stops at the
        /// guard's own result and the reader has to open the graph and work out which input did it.
        /// </para>
        /// </summary>
        private static GuardTrace AddTraceClause(
            IBehaviorTreeRecording recording, int flipIndex, List<BehaviorTreeExplanationClause> clauses)
        {
            var flipped = recording.EventAt(flipIndex);
            var trace = recording.TraceFor(flipped.Tick, flipped.Sequence);

            if (trace == null || trace.Chain.Count <= 1) return trace;

            clauses.Add(new BehaviorTreeExplanationClause(
                BehaviorTreeClauseRole.Cause,
                $"It read {trace.Describe()}",
                BehaviorTreeExplanationLink.ToGuard(trace.GuardGuid, trace.CallSiteId, trace.Tick, trace.Sequence)));

            return trace;
        }

        /// <summary>Which sibling ran instead, when the topology can say who the siblings are.</summary>
        private static void AddSiblingClause(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, List<BehaviorTreeExplanationClause> clauses)
        {
            if (topology == null) return;
            if (!topology.TryGetNode(nodeGuid, out var node) || node.ParentGuid == Guid.Empty) return;
            if (!topology.TryGetNode(node.ParentGuid, out var parent) || parent.Children.Count <= 1) return;

            var priority = IndexOf(parent.Children, nodeGuid);
            if (priority < 0) return;

            for (int i = 0; i < parent.Children.Count; i++)
            {
                if (i == priority) continue;

                var sibling = parent.Children[i];
                var entered = LastIndexOf(recording, callSiteId, sibling, BehaviorTreeEventKind.NodeEnter, recording.EventCount, atTick);
                if (entered < 0) continue;

                // Only a sibling that outranks this node explains it not running. A lower-priority sibling
                // running is a consequence of this one failing, not a reason it never started.
                if (i > priority) continue;

                var enteredEvent = recording.EventAt(entered);
                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Cause,
                    $"The {parent.TypeName} above it ran '{NameOf(topology, sibling)}' (priority {i + 1}) at tick {enteredEvent.Tick}; this node is priority {priority + 1}.",
                    BehaviorTreeExplanationLink.ToNode(sibling, callSiteId, enteredEvent.Tick, enteredEvent.Sequence)));

                return;
            }
        }

        private static void AddActiveGuardsClause(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int atTick, List<BehaviorTreeExplanationClause> clauses)
        {
            // Guards name their owner on every eval, so the recording alone knows which guards protect this
            // node — no topology needed.
            var seen = new List<Guid>();

            for (int i = recording.EventCount - 1; i >= 0; i--)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Kind != BehaviorTreeEventKind.GuardEval) continue;
                if (recorded.CallSiteId != callSiteId || recorded.RelatedGuid != nodeGuid) continue;
                if (recorded.Tick > atTick || seen.Contains(recorded.NodeGuid)) continue;

                seen.Add(recorded.NodeGuid);

                clauses.Add(new BehaviorTreeExplanationClause(
                    BehaviorTreeClauseRole.Context,
                    $"Guard '{NameOf(topology, recorded.NodeGuid)}' has been {(recorded.Flag ? "true" : "false")} since tick {recorded.Tick}.",
                    BehaviorTreeExplanationLink.ToGuard(recorded.NodeGuid, callSiteId, recorded.Tick, recorded.Sequence)));
            }
        }

        /// <summary>
        /// The classic behavior tree bug: a branch that enters, aborts and re-enters every few ticks. Cheap to
        /// spot from the buffer, and worth saying here because the reader is already looking at the node.
        /// </summary>
        private static void AddOscillationClause(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, int atTick,
            List<BehaviorTreeExplanationClause> clauses)
        {
            int enters = 0;
            int aborts = 0;
            var from = atTick - OscillationWindow;

            for (int i = 0; i < recording.EventCount; i++)
            {
                var recorded = recording.EventAt(i);
                if (recorded.CallSiteId != callSiteId || recorded.NodeGuid != nodeGuid) continue;
                if (recorded.Tick < from || recorded.Tick > atTick) continue;

                if (recorded.Kind == BehaviorTreeEventKind.NodeEnter) enters++;
                else if (recorded.Kind == BehaviorTreeEventKind.NodeAborted) aborts++;
            }

            if (aborts < OscillationThreshold) return;

            clauses.Add(new BehaviorTreeExplanationClause(
                BehaviorTreeClauseRole.Context,
                $"Oscillating: entered {enters} times and aborted {aborts} times in the last {OscillationWindow} ticks."));
        }

        private static void AddClippedCaveat(IBehaviorTreeRecording recording, List<BehaviorTreeExplanationClause> clauses)
        {
            if (recording.Dropped <= 0) return;

            clauses.Add(new BehaviorTreeExplanationClause(
                BehaviorTreeClauseRole.Caveat,
                $"The recording is clipped: {recording.Dropped} older events were dropped, so anything before the buffer's start is not accounted for."));
        }

        #endregion

        #region Scanning

        /// <summary>
        /// An aborted node exits in the same tick it was aborted, and the exit is recorded second.
        ///
        /// <para>
        /// Taken at face value the last event is a plain <c>NodeExit Failure</c>, which is true and useless:
        /// it names the status and loses the guard that caused it, in exactly the case the reader most needs
        /// the guard. The abort is the story, so when both are present for the same node in the same tick the
        /// abort wins. The resulting status is not lost — it becomes a clause on the abort.
        /// </para>
        /// </summary>
        /// <summary>
        /// An abort or a takeover is followed by the node's own exit on the same tick, and the exit is the
        /// less interesting half: it says the node stopped, where the earlier event says why. Explain that
        /// one instead.
        /// </summary>
        private static int PreferCauseOverItsOwnExit(IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, int episode)
        {
            var exit = recording.EventAt(episode);
            if (exit.Kind != BehaviorTreeEventKind.NodeExit) return episode;

            for (int i = episode - 1; i >= 0; i--)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Tick != exit.Tick) break;
                if (recorded.CallSiteId != callSiteId || recorded.NodeGuid != nodeGuid) continue;

                // An enter in the same tick means this exit belongs to a later episode than any earlier cause.
                if (recorded.Kind == BehaviorTreeEventKind.NodeEnter) break;
                if (recorded.Kind == BehaviorTreeEventKind.NodeAborted) return i;
                if (recorded.Kind == BehaviorTreeEventKind.NodeTakenOver) return i;
            }

            return episode;
        }

        /// <summary>The exit an abort produced, when the recording caught it.</summary>
        private static int ExitAfterAbort(IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, int abortIndex)
        {
            var abort = recording.EventAt(abortIndex);

            for (int i = abortIndex + 1; i < recording.EventCount; i++)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Tick != abort.Tick) return -1;
                if (recorded.CallSiteId != callSiteId || recorded.NodeGuid != nodeGuid) continue;

                if (recorded.Kind == BehaviorTreeEventKind.NodeExit) return i;
            }

            return -1;
        }

        /// <summary>
        /// The node's first lifecycle event *after* the vantage point, or -1.
        ///
        /// <para>
        /// The counterpart to <see cref="LastLifecycleIndex"/>, and the only place the explainer deliberately
        /// looks forward. It never reports what happened there — that would leak the future the scrubber is
        /// trying to hide — only that something does, which is what separates "has not started yet" from
        /// "never ran at all".
        /// </para>
        /// </summary>
        private static int FirstLifecycleIndexAfter(IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, int atTick)
        {
            for (int i = 0; i < recording.EventCount; i++)
            {
                var recorded = recording.EventAt(i);
                if (recorded.CallSiteId != callSiteId || recorded.NodeGuid != nodeGuid || recorded.Tick <= atTick) continue;

                switch (recorded.Kind)
                {
                    case BehaviorTreeEventKind.NodeEnter:
                    case BehaviorTreeEventKind.NodeExit:
                    case BehaviorTreeEventKind.NodeAborted:
                    case BehaviorTreeEventKind.NodeSkipped:
                    case BehaviorTreeEventKind.NodeTakenOver:
                        return i;
                }
            }

            return -1;
        }

        private static int LastLifecycleIndex(IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, int atTick)
        {
            for (int i = recording.EventCount - 1; i >= 0; i--)
            {
                var recorded = recording.EventAt(i);
                if (recorded.CallSiteId != callSiteId || recorded.NodeGuid != nodeGuid || recorded.Tick > atTick) continue;

                switch (recorded.Kind)
                {
                    case BehaviorTreeEventKind.NodeEnter:
                    case BehaviorTreeEventKind.NodeExit:
                    case BehaviorTreeEventKind.NodeAborted:
                    case BehaviorTreeEventKind.NodeSkipped:
                    case BehaviorTreeEventKind.NodeTakenOver:
                        return i;
                }
            }

            return -1;
        }

        private static int LastIndexOf(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, BehaviorTreeEventKind kind,
            int before, int atTick = int.MaxValue)
        {
            for (int i = Math.Min(before, recording.EventCount) - 1; i >= 0; i--)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Kind != kind || recorded.CallSiteId != callSiteId || recorded.NodeGuid != nodeGuid) continue;
                if (recorded.Tick > atTick) continue;

                return i;
            }

            return -1;
        }

        private static int LastGuardEvalIndex(IBehaviorTreeRecording recording, int callSiteId, Guid guardGuid, int atTick)
        {
            return LastIndexOf(recording, callSiteId, guardGuid, BehaviorTreeEventKind.GuardEval, recording.EventCount, atTick);
        }

        /// <summary>
        /// The most recent recorded change of a guard to a given value. Guards are recorded on transition
        /// only, so this is also the moment it became that value — the repeats that would otherwise have to be
        /// skipped were never written.
        /// </summary>
        private static int LastGuardTransitionIndex(
            IBehaviorTreeRecording recording, int callSiteId, Guid guardGuid, int before, bool value)
        {
            if (guardGuid == Guid.Empty) return -1;

            for (int i = Math.Min(before, recording.EventCount) - 1; i >= 0; i--)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Kind != BehaviorTreeEventKind.GuardEval) continue;
                if (recorded.CallSiteId != callSiteId || recorded.NodeGuid != guardGuid) continue;
                if (recorded.Flag != value) continue;

                return i;
            }

            return -1;
        }

        private static int CountGuardTransitions(IBehaviorTreeRecording recording, int callSiteId, Guid guardGuid, int fromTick, int toTick)
        {
            int count = 0;

            for (int i = 0; i < recording.EventCount; i++)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Kind != BehaviorTreeEventKind.GuardEval) continue;
                if (recorded.CallSiteId != callSiteId || recorded.NodeGuid != guardGuid) continue;
                if (recorded.Tick < fromTick || recorded.Tick > toTick) continue;

                count++;
            }

            return count;
        }

        /// <summary>
        /// The most recent variable write before an index, optionally restricted to a set of keys. Not
        /// restricted by call site: a guard inside a branch commonly reads agent state written somewhere else
        /// entirely, which is the case the why-inspector exists to explain.
        /// </summary>
        private static int LastWriteIndex(IBehaviorTreeRecording recording, int before, IReadOnlyList<string> keys)
        {
            for (int i = Math.Min(before, recording.EventCount) - 1; i >= 0; i--)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Kind != BehaviorTreeEventKind.VariableWrite) continue;
                if (keys != null && !Contains(keys, recorded.Key)) continue;

                return i;
            }

            return -1;
        }

        private static int LastChildExitIndex(
            IBehaviorTreeRecording recording, int callSiteId, Guid nodeGuid, IBehaviorTreeTopology topology,
            int before, int tick)
        {
            if (topology == null || !topology.TryGetNode(nodeGuid, out var node) || node.Children.Count == 0) return -1;

            for (int i = Math.Min(before, recording.EventCount) - 1; i >= 0; i--)
            {
                var recorded = recording.EventAt(i);
                if (recorded.Kind != BehaviorTreeEventKind.NodeExit) continue;
                if (recorded.CallSiteId != callSiteId || recorded.Tick != tick) continue;
                if (IndexOf(node.Children, recorded.NodeGuid) < 0) continue;

                return i;
            }

            return -1;
        }

        #endregion

        #region Naming

        private static string NameOf(IBehaviorTreeTopology topology, Guid guid)
        {
            if (guid == Guid.Empty) return "(none)";

            if (topology != null && topology.TryGetNode(guid, out var node) && !string.IsNullOrEmpty(node.DisplayName))
            {
                return node.DisplayName;
            }

            return ShortGuid(guid);
        }

        private static string WriterName(in BehaviorTreeEvent written, IBehaviorTreeTopology topology)
        {
            if (written.RelatedGuid != Guid.Empty) return NameOf(topology, written.RelatedGuid);
            if (!string.IsNullOrEmpty(written.Writer)) return written.Writer;

            return "(unknown writer)";
        }

        /// <summary>Enough of a guid to recognise it in a dump, short enough to read in a sentence.</summary>
        public static string ShortGuid(Guid guid) => guid == Guid.Empty ? "(none)" : guid.ToString("N").Substring(0, 8);

        /// <summary>The chain of call sites down to this one, e.g. <c>Zombie -> Combat -> Attack</c>.</summary>
        public static string CallSitePath(IBehaviorTreeRecording recording, int callSiteId)
        {
            var callSites = recording.CallSites;
            if (callSites == null || callSites.Count == 0) return string.Empty;

            var names = new List<string>();
            var id = callSiteId;

            // Bounded by the number of call sites: a malformed parent chain must not spin here.
            for (int guard = 0; guard < callSites.Count; guard++)
            {
                var callSite = FindCallSite(callSites, id);
                if (!callSite.HasValue) break;

                names.Add(string.IsNullOrEmpty(callSite.Value.AssetName) ? "(unnamed)" : callSite.Value.AssetName);

                if (callSite.Value.IsRoot) break;
                id = callSite.Value.ParentId;
            }

            var path = new StringBuilder();
            for (int i = names.Count - 1; i >= 0; i--)
            {
                if (path.Length > 0) path.Append(" -> ");
                path.Append(names[i]);
            }

            return path.ToString();
        }

        private static BehaviorTreeCallSite? FindCallSite(IReadOnlyList<BehaviorTreeCallSite> callSites, int id)
        {
            for (int i = 0; i < callSites.Count; i++)
            {
                if (callSites[i].Id == id) return callSites[i];
            }

            return null;
        }

        private static int IndexOf(IReadOnlyList<Guid> guids, Guid guid)
        {
            for (int i = 0; i < guids.Count; i++)
            {
                if (guids[i] == guid) return i;
            }

            return -1;
        }

        private static bool Contains(IReadOnlyList<string> keys, string key)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (string.Equals(keys[i], key, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static string Join(IReadOnlyList<string> keys)
        {
            var text = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0) text.Append(i == keys.Count - 1 ? " or " : ", ");
                text.Append('\'').Append(keys[i]).Append('\'');
            }

            return text.ToString();
        }

        #endregion
    }
}

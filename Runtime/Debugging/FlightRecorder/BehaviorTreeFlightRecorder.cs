using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Observes and never participates: nothing here may change what the tree decides, and every entry
    /// point tolerates a node with no machine, no scope or a half-built graph.
    /// </summary>
    public sealed class BehaviorTreeFlightRecorder : IBehaviorTreeRecording
    {
        public const int DefaultEventCapacity = 2048;

        private readonly BehaviorTreeEventRing events;
        private readonly GuardTraceRing guardTraces;
        private readonly List<BehaviorTreeCallSite> callSites = new();
        private readonly Dictionary<BehaviorTreeVariableScope, int> callSiteIdsByScope = new();
        private readonly Dictionary<GuardAtCallSite, bool> lastGuardResults = new();

        private int sequenceWithinTick;

        public BehaviorTreeFlightRecorder(
            string agentName,
            string treeName,
            int capacity = DefaultEventCapacity,
            int traceCapacity = GuardTraceRing.DefaultCapacity)
        {
            AgentName = agentName;
            TreeName = treeName;
            events = new BehaviorTreeEventRing(capacity);
            guardTraces = new GuardTraceRing(traceCapacity);

            callSites.Add(new BehaviorTreeCallSite(
                BehaviorTreeCallSite.RootId, BehaviorTreeCallSite.RootId, Guid.Empty, treeName));
        }

        public string AgentName { get; }

        public string TreeName { get; }

        public int Tick { get; private set; }

        public bool IsEnabled { get; set; } = true;

        public bool IsTracingEnabled { get; set; } = true;

        public bool IsRecording => IsEnabled && BehaviorTreeFlightRecorderRegistry.IsGloballyEnabled;

        public bool IsTracing =>
            IsRecording && IsTracingEnabled && BehaviorTreeFlightRecorderRegistry.IsTracingGloballyEnabled;

        public BehaviorTreeEventRing Events => events;

        public GuardTraceRing Traces => guardTraces;

        public IReadOnlyList<BehaviorTreeCallSite> CallSites => callSites;

        public int EventCount => events.Count;

        public BehaviorTreeEvent EventAt(int index) => events[index];

        public int Dropped => events.DroppedEventCount;

        public int TraceCount => guardTraces.Count;

        public GuardTrace TraceAt(int index) => guardTraces[index];

        public GuardTrace TraceFor(int tick, int sequence) => guardTraces.Find(tick, sequence);

        public void BeginTick()
        {
            Tick++;
            sequenceWithinTick = 0;
        }

        public int RegisterCallSite(
            BehaviorTreeVariableScope scope,
            BehaviorTreeVariableScope parentScope,
            Guid runNodeGuid,
            string assetName)
        {
            if (scope == null) return BehaviorTreeCallSite.RootId;

            if (callSiteIdsByScope.TryGetValue(scope, out var alreadyRegistered)) return alreadyRegistered;

            var id = callSites.Count;

            callSiteIdsByScope[scope] = id;
            callSites.Add(new BehaviorTreeCallSite(id, CallSiteIdOf(parentScope), runNodeGuid, assetName));

            return id;
        }

        public void BindRootScope(BehaviorTreeVariableScope scope)
        {
            if (scope == null) return;

            callSiteIdsByScope[scope] = BehaviorTreeCallSite.RootId;
        }

        public int CallSiteOf(BehaviorTreeNode node) => CallSiteIdOf(node?.VariableScope);

        private int CallSiteIdOf(BehaviorTreeVariableScope scope)
        {
            if (scope == null) return BehaviorTreeCallSite.RootId;

            return callSiteIdsByScope.TryGetValue(scope, out var id) ? id : BehaviorTreeCallSite.RootId;
        }

        public void Clear()
        {
            events.Clear();
            guardTraces.Clear();
            lastGuardResults.Clear();
            sequenceWithinTick = 0;
        }

        public void NodeEnter(BehaviorTreeNode node)
        {
            if (!IsRecording || node == null) return;

            var callSiteId = CallSiteOf(node);

            Record(BehaviorTreeEvent.NodeEntered(NextStamp(callSiteId), node.guid));
            RecordSubTreeEntered(node, callSiteId);
        }

        public void NodeExit(BehaviorTreeNode node, ExecutionStatus status)
        {
            if (!IsRecording || node == null) return;

            var callSiteId = CallSiteOf(node);

            RecordSubTreeLeft(node, callSiteId);
            Record(BehaviorTreeEvent.NodeExited(NextStamp(callSiteId), node.guid, status));
        }

        private void RecordSubTreeEntered(BehaviorTreeNode node, int callSiteId)
        {
            if (node is RunBehaviorTreeGraphNode runNode)
            {
                Record(BehaviorTreeEvent.SubTreePushed(
                    NextStamp(callSiteId), node.guid, BehaviorTreeRecordingNames.AssetNameOf(runNode)));
            }
        }

        private void RecordSubTreeLeft(BehaviorTreeNode node, int callSiteId)
        {
            if (node is RunBehaviorTreeGraphNode runNode)
            {
                Record(BehaviorTreeEvent.SubTreePopped(
                    NextStamp(callSiteId), node.guid, BehaviorTreeRecordingNames.AssetNameOf(runNode)));
            }
        }

        public void NodeSkipped(BehaviorTreeNode node, ConditionalExecution guard)
        {
            if (!IsRecording || node == null) return;

            Record(BehaviorTreeEvent.NodeSkippedByGuard(
                NextStamp(CallSiteOf(node)), node.guid, GuidOf(guard)));
        }

        public void NodeAborted(BehaviorTreeNode node, ConditionalExecution guard)
        {
            if (!IsRecording || node == null) return;

            Record(BehaviorTreeEvent.NodeAbortedByGuard(
                NextStamp(CallSiteOf(node)), node.guid, GuidOf(guard)));
        }

        public void NodeTakenOver(
            BehaviorTreeNode victim, BehaviorTreeNode preemptor, ConditionalExecution guard)
        {
            if (!IsRecording || victim == null) return;

            Record(BehaviorTreeEvent.NodeTakenOver(
                NextStamp(CallSiteOf(victim)),
                victim.guid,
                GuidOf(guard),
                NameOf(preemptor),
                GuidOf(preemptor)));
        }

        /// <summary>
        /// Guards evaluate every tick while their owner runs, so only a change is kept: the repeats would
        /// push the transitions out of the buffer.
        /// </summary>
        public void GuardEval(BehaviorTreeNode guardedNode, ConditionalExecution guard, bool result)
        {
            if (!IsRecording || guardedNode == null || guard == null) return;

            var callSiteId = CallSiteOf(guardedNode);
            var guardAtCallSite = new GuardAtCallSite(callSiteId, guard.guid);

            if (IsSameAsLastResult(guardAtCallSite, result)) return;

            lastGuardResults[guardAtCallSite] = result;

            var stamp = NextStamp(callSiteId);

            Record(BehaviorTreeEvent.GuardChanged(stamp, guard.guid, guardedNode.guid, result));
            CaptureTrace(guardedNode, guard, result, stamp);
        }

        private bool IsSameAsLastResult(in GuardAtCallSite guardAtCallSite, bool result)
        {
            return lastGuardResults.TryGetValue(guardAtCallSite, out var previous) && previous == result;
        }

        private void CaptureTrace(
            BehaviorTreeNode guardedNode,
            ConditionalExecution guard,
            bool result,
            in BehaviorTreeEventStamp stamp)
        {
            if (!IsTracing) return;

            guardTraces.Add(GuardTraceCapture.Capture(
                guardedNode, guard, result, stamp.Tick, stamp.Sequence, stamp.CallSiteId));
        }

        public void VariableWrite(
            BehaviorTreeNode writer,
            string key,
            BehaviorTreeVariableKind variableKind,
            object oldValue,
            object newValue)
        {
            if (!IsRecording || string.IsNullOrEmpty(key)) return;

            Record(
                BehaviorTreeEvent.VariableWrittenByNode(
                    NextStamp(CallSiteOf(writer)),
                    GuidOf(writer),
                    key,
                    variableKind,
                    BehaviorTreeEvent.DescribeValue(oldValue),
                    BehaviorTreeEvent.DescribeValue(newValue)),
                newValue);
        }

        /// <summary>
        /// Unused inside BH3, and kept because sensors outside the tree publish most of the facts a tree
        /// reads: without it, who wrote the value that flipped a guard has no answer in that common case.
        /// </summary>
        public void ExternalVariableWrite(
            string writerName,
            string key,
            object oldValue,
            object newValue,
            BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.Object)
        {
            if (!IsRecording || string.IsNullOrEmpty(key)) return;

            Record(
                BehaviorTreeEvent.VariableWrittenOutsideTree(
                    NextStamp(BehaviorTreeCallSite.RootId),
                    NameOfExternalWriter(writerName),
                    key,
                    variableKind,
                    BehaviorTreeEvent.DescribeValue(oldValue),
                    BehaviorTreeEvent.DescribeValue(newValue)),
                newValue);
        }

        private BehaviorTreeEventStamp NextStamp(int callSiteId)
        {
            return new BehaviorTreeEventStamp(
                Tick, sequenceWithinTick++, UnityEngine.Time.frameCount, UnityEngine.Time.time, callSiteId);
        }

        private void Record(in BehaviorTreeEvent recorded, object valueForBreakpointMatching = null)
        {
            events.Add(recorded);

            // After the ring, never before: a breakpoint hit pauses the editor and parks the scrubber on this
            // tick, so the event that caused it has to already be in the recording the reader will look at.
            BehaviorTreeBreakpoints.Evaluate(this, recorded, valueForBreakpointMatching);
        }

        private static Guid GuidOf(ConditionalExecution guard) => guard?.guid ?? Guid.Empty;

        private static Guid GuidOf(BehaviorTreeNode node) => node?.guid ?? Guid.Empty;

        private static string NameOf(BehaviorTreeNode node) => node != null ? node.NodeName : null;

        private static string NameOfExternalWriter(string writerName)
        {
            return string.IsNullOrEmpty(writerName)
                ? BehaviorTreeRecordingNames.UnnamedExternalWriter
                : writerName;
        }

        private readonly struct GuardAtCallSite : IEquatable<GuardAtCallSite>
        {
            private readonly int callSiteId;
            private readonly Guid guard;

            public GuardAtCallSite(int callSiteId, Guid guard)
            {
                this.callSiteId = callSiteId;
                this.guard = guard;
            }

            public bool Equals(GuardAtCallSite other) =>
                callSiteId == other.callSiteId && guard.Equals(other.guard);

            public override bool Equals(object obj) => obj is GuardAtCallSite other && Equals(other);

            public override int GetHashCode() => unchecked((callSiteId * 397) ^ guard.GetHashCode());
        }
    }
}

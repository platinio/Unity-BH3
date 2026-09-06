using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// The per-agent black box. Records what the tree did, so "why did it do that" can be read off a
    /// recording instead of reconstructed from a hunch and a breakpoint.
    ///
    /// <para>
    /// It observes; it does not participate. Nothing here can change what the tree decides, and every entry
    /// point tolerates being called on a node with no machine, no scope, or a half-built graph — a debugging
    /// aid that can break the thing it watches is worse than no debugging aid.
    /// </para>
    ///
    /// <para>
    /// Call sites reach this through <see cref="BehaviorTreeRecorder"/>, which compiles away outside the
    /// editor and dev builds. Reach for that rather than calling a recorder directly from runtime code.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeFlightRecorder : IBehaviorTreeRecording
    {
        /// <summary>
        /// Events, not ticks. The spec asks for "~2000 ticks", but a tick produces anywhere from zero to a
        /// dozen events, so sizing by tick would make the memory cost a function of how busy the tree is.
        /// At roughly a hundred bytes an event this is about 200 KB per recorded agent.
        /// </summary>
        public const int DefaultCapacity = 2048;

        private readonly BehaviorTreeEventRing ring;

        /// <summary>
        /// Guard traces, in their own much shorter ring. Kept apart from the events because a trace is a
        /// variable-length chain and the event ring's flatness is what keeps recording allocation-free.
        /// </summary>
        private readonly GuardTraceRing traces;

        private readonly List<BehaviorTreeCallSite> callSites = new();

        /// <summary>
        /// Scope object to call-site id. Reference identity is exactly what we want: one scope instance is
        /// created per call site, so two branches sharing an asset hash differently.
        /// </summary>
        private readonly Dictionary<BehaviorTreeVariableScope, int> callSiteIds = new();

        /// <summary>
        /// The last result seen from each guard, so only transitions get recorded. Keyed by call site as well
        /// as guid for the same reason everything else is: one guard guid, two live copies.
        /// </summary>
        private readonly Dictionary<GuardKey, bool> lastGuardResults = new();

        private int sequence;

        public BehaviorTreeFlightRecorder(
            string agentName, string treeName, int capacity = DefaultCapacity, int traceCapacity = GuardTraceRing.DefaultCapacity)
        {
            AgentName = agentName;
            TreeName = treeName;
            ring = new BehaviorTreeEventRing(capacity);
            traces = new GuardTraceRing(traceCapacity);

            callSites.Add(new BehaviorTreeCallSite(
                BehaviorTreeCallSite.RootId, BehaviorTreeCallSite.RootId, Guid.Empty, treeName));
        }

        /// <summary>The agent this is recording, for the multi-agent view and the export header.</summary>
        public string AgentName { get; }

        /// <summary>The root tree's asset name.</summary>
        public string TreeName { get; }

        /// <summary>
        /// Whether events are being kept. Flipping this off leaves the buffer intact, which is what you want
        /// after reproducing a bug: stop recording so the evidence stops scrolling away.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Machine ticks since this agent started. Advanced by <see cref="BeginTick"/>.</summary>
        public int Tick { get; private set; }

        public BehaviorTreeEventRing Events => ring;

        public IReadOnlyList<BehaviorTreeCallSite> CallSites => callSites;

        /// <summary>
        /// A live recorder is also a readable recording, so the why-inspector reads an agent that is still
        /// running through exactly the same interface it uses for a recording loaded from a file. Nothing here
        /// copies: these forward straight to the ring.
        /// </summary>
        public int EventCount => ring.Count;

        public BehaviorTreeEvent EventAt(int index) => ring[index];

        public int Dropped => ring.Dropped;

        public GuardTrace TraceFor(int tick, int sequence) => traces.Find(tick, sequence);

        public int TraceCount => traces.Count;

        public GuardTrace TraceAt(int index) => traces[index];

        /// <summary>
        /// True when this recorder is actually keeping events. The global switch is checked here rather than
        /// at every call site so there is one answer to "is anything being recorded".
        /// </summary>
        public bool IsRecording => Enabled && BehaviorTreeFlightRecorders.GloballyEnabled;

        /// <summary>
        /// Whether guard traces are being captured. Separate from <see cref="Enabled"/> because they have
        /// very different costs: an event is a struct written into a pre-allocated slot, while a trace
        /// re-pulls a chain of ports and walks a script graph. A scene with two hundred agents can keep the
        /// events and drop the traces.
        /// </summary>
        public bool TracingEnabled { get; set; } = true;

        public bool IsTracing => IsRecording && TracingEnabled && BehaviorTreeFlightRecorders.TracingGloballyEnabled;

        /// <summary>Guard traces, oldest first.</summary>
        public GuardTraceRing Traces => traces;

        /// <summary>
        /// Opens a new tick. The machine calls this once per <c>Update</c>, before the tree runs.
        /// <para>
        /// The tick is counted per agent rather than taken from <c>Time.frameCount</c> because an agent can
        /// be spawned, disabled or switched to a different tree mid-session, and a recording that starts at
        /// frame 41,207 is harder to read than one that starts at zero. The frame is recorded on every event
        /// anyway, so two agents' recordings can still be lined up.
        /// </para>
        /// </summary>
        public void BeginTick()
        {
            Tick++;
            sequence = 0;
        }

        /// <summary>
        /// Registers a call site, or returns the id it already has. Called as the machine builds variable
        /// scopes, before the tree runs.
        /// </summary>
        public int RegisterCallSite(BehaviorTreeVariableScope scope, BehaviorTreeVariableScope parentScope, Guid runNodeGuid, string assetName)
        {
            if (scope == null) return BehaviorTreeCallSite.RootId;

            if (callSiteIds.TryGetValue(scope, out var existing)) return existing;

            var id = callSites.Count;
            var parentId = parentScope != null && callSiteIds.TryGetValue(parentScope, out var found)
                ? found
                : BehaviorTreeCallSite.RootId;

            callSiteIds[scope] = id;
            callSites.Add(new BehaviorTreeCallSite(id, parentId, runNodeGuid, assetName));

            return id;
        }

        /// <summary>
        /// Binds the root tree's scope to <see cref="BehaviorTreeCallSite.RootId"/>. Separate from
        /// <see cref="RegisterCallSite"/> because the root already exists — it is created in the constructor,
        /// where the tree's name is known but its scope is not.
        /// </summary>
        public void BindRootScope(BehaviorTreeVariableScope scope)
        {
            if (scope == null) return;

            callSiteIds[scope] = BehaviorTreeCallSite.RootId;
        }

        /// <summary>Which call site a node is in, or the root when nothing registered its scope.</summary>
        public int CallSiteOf(BehaviorTreeNode node)
        {
            var scope = node?.VariableScope;
            if (scope == null) return BehaviorTreeCallSite.RootId;

            return callSiteIds.TryGetValue(scope, out var id) ? id : BehaviorTreeCallSite.RootId;
        }

        public void Clear()
        {
            ring.Clear();
            traces.Clear();
            lastGuardResults.Clear();
            sequence = 0;
        }

        #region Recording

        public void NodeEnter(BehaviorTreeNode node)
        {
            if (!IsRecording || node == null) return;

            var callSite = CallSiteOf(node);
            Add(BehaviorTreeEventKind.NodeEnter, callSite, node.guid);

            // A run node entering *is* a sub-tree being pushed. Emitting both keeps the timeline's lane
            // structure readable without the reader having to know which node types nest.
            if (node is RunBehaviorTreeGraphNode runNode)
            {
                Add(BehaviorTreeEventKind.TreePushed, callSite, node.guid,
                    key: runNode.BehaviorTreeGraphAsset != null ? runNode.BehaviorTreeGraphAsset.name : "(none assigned)");
            }
        }

        public void NodeExit(BehaviorTreeNode node, ExecutionStatus status)
        {
            if (!IsRecording || node == null) return;

            var callSite = CallSiteOf(node);

            if (node is RunBehaviorTreeGraphNode runNode)
            {
                Add(BehaviorTreeEventKind.TreePopped, callSite, node.guid,
                    key: runNode.BehaviorTreeGraphAsset != null ? runNode.BehaviorTreeGraphAsset.name : "(none assigned)");
            }

            Add(BehaviorTreeEventKind.NodeExit, callSite, node.guid, status: status);
        }

        /// <summary>
        /// A guard result, recorded only when it differs from the last one seen. Guards evaluate every tick
        /// while their owner runs; the transitions are the information and the repeats are noise that would
        /// push the interesting events out of the buffer.
        /// </summary>
        public void GuardEval(BehaviorTreeNode owner, ConditionalExecution guard, bool result)
        {
            if (!IsRecording || owner == null || guard == null) return;

            var callSite = CallSiteOf(owner);
            var key = new GuardKey(callSite, guard.guid);

            if (lastGuardResults.TryGetValue(key, out var previous) && previous == result) return;

            lastGuardResults[key] = result;

            // The sequence is read before Add stamps it, so the trace and the event agree on which moment
            // this was. That pairing is the only thing linking them.
            var eventSequence = sequence;

            Add(BehaviorTreeEventKind.GuardEval, callSite, guard.guid, relatedGuid: owner.guid, flag: result);

            // Only here, on a transition. Guards evaluate every tick and a trace costs far more than an
            // event, so steady state stays free while the moments worth explaining are kept.
            if (IsTracing)
            {
                traces.Add(GuardTraceCapture.Capture(owner, guard, result, Tick, eventSequence, callSite));
            }
        }

        /// <summary>A guard was false as its owner was about to start, so the owner never ran.</summary>
        public void NodeSkipped(BehaviorTreeNode node, ConditionalExecution guard)
        {
            if (!IsRecording || node == null) return;

            Add(BehaviorTreeEventKind.NodeSkipped, CallSiteOf(node), node.guid, relatedGuid: guard?.guid ?? Guid.Empty);
        }

        /// <summary>A guard turned false mid-run, so its owner failed this tick and the branch under it died.</summary>
        public void NodeAborted(BehaviorTreeNode node, ConditionalExecution guard)
        {
            if (!IsRecording || node == null) return;

            Add(BehaviorTreeEventKind.NodeAborted, CallSiteOf(node), node.guid, relatedGuid: guard?.guid ?? Guid.Empty);
        }

        /// <summary>
        /// A running node stopped because a higher-priority sibling took the slot.
        /// <para>
        /// <paramref name="guard"/> goes in <c>RelatedGuid</c> so that field means "the guard responsible"
        /// across every event that ends a branch, and the preemptor's name goes in <c>Key</c> — which is
        /// what lets an explanation read "preempted by 'Attack'" rather than quoting a guid.
        /// </para>
        /// </summary>
        public void NodeTakenOver(BehaviorTreeNode victim, BehaviorTreeNode preemptor, ConditionalExecution guard)
        {
            if (!IsRecording || victim == null) return;

            Add(BehaviorTreeEventKind.NodeTakenOver,
                CallSiteOf(victim),
                victim.guid,
                relatedGuid: guard?.guid ?? Guid.Empty,
                key: preemptor != null ? preemptor.NodeName : null,
                newValue: preemptor != null ? preemptor.guid.ToString() : null);
        }

        /// <summary>
        /// A variable written by a node in the tree. <paramref name="variableKind"/> is which store it landed
        /// in, which the call site cannot answer — see <see cref="BehaviorTreeEvent.VariableKind"/>.
        /// </summary>
        public void VariableWrite(
            BehaviorTreeNode writer, string key, BehaviorTreeVariableKind variableKind,
            object oldValue, object newValue)
        {
            if (!IsRecording || string.IsNullOrEmpty(key)) return;

            Add(BehaviorTreeEventKind.VariableWrite,
                CallSiteOf(writer),
                nodeGuid: Guid.Empty,
                relatedGuid: writer?.guid ?? Guid.Empty,
                key: key,
                oldValue: BehaviorTreeEvent.Describe(oldValue),
                newValue: BehaviorTreeEvent.Describe(newValue),
                variableKind: variableKind,
                writtenValue: newValue);
        }

        /// <summary>
        /// A variable written by something outside the tree — a perception sensor, or anything else that
        /// publishes agent state.
        /// <para>
        /// Public and unused by BH3 itself on purpose. The facts a tree reads are mostly produced by always-on
        /// sensors rather than by branches, so without this the one question the recorder exists to answer —
        /// "who changed the value that flipped this guard?" — has no answer in the common case. A sensor calls
        /// it with its own name; there is no node guid to give.
        /// </para>
        /// <para>
        /// The kind defaults to <see cref="BehaviorTreeVariableKind.Object"/> because a writer outside the
        /// tree has no branch scope to write into: agent state is the only store it can reach through the
        /// machine.
        /// </para>
        /// </summary>
        public void ExternalVariableWrite(
            string writerName,
            string key,
            object oldValue,
            object newValue,
            BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.Object)
        {
            if (!IsRecording || string.IsNullOrEmpty(key)) return;

            Add(BehaviorTreeEventKind.VariableWrite,
                BehaviorTreeCallSite.RootId,
                nodeGuid: Guid.Empty,
                key: key,
                oldValue: BehaviorTreeEvent.Describe(oldValue),
                newValue: BehaviorTreeEvent.Describe(newValue),
                variableKind: variableKind,
                writer: string.IsNullOrEmpty(writerName) ? "(external)" : writerName,
                writtenValue: newValue);
        }

        private void Add(
            BehaviorTreeEventKind kind,
            int callSite,
            Guid nodeGuid,
            Guid relatedGuid = default,
            ExecutionStatus status = ExecutionStatus.None,
            bool flag = false,
            string key = null,
            string oldValue = null,
            string newValue = null,
            BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.None,
            string writer = null,
            object writtenValue = null)
        {
            var recorded = BehaviorTreeEvent.Create(
                kind, Tick, sequence++, UnityEngine.Time.frameCount, UnityEngine.Time.time,
                callSite, nodeGuid, relatedGuid, status, flag, key, oldValue, newValue, variableKind, writer);

            ring.Add(recorded);

            // Breakpoints hang off this one call rather than off the six methods above it, because by the time
            // an event reaches here it already carries everything they match on. Guard breakpoints inherit the
            // transition filtering that way: GuardEval is only reached when the answer actually changed, so a
            // guard sitting false for four hundred ticks cannot pause the editor four hundred times.
            //
            // After the ring and never before. A hit pauses the editor and parks the scrubber on this tick, so
            // the event that caused it has to already be in the recording the reader is about to look at.
            //
            // writtenValue is the live object on a variable write, and null on everything else. It is handed
            // to the matcher and never to the ring, which is the distinction that lets `ammo < 5` compare
            // numbers while the recording still keeps only strings — a reference held in the ring would go on
            // mutating after the event was recorded.
            BehaviorTreeBreakpoints.Evaluate(this, recorded, writtenValue);
        }

        #endregion

        private readonly struct GuardKey : IEquatable<GuardKey>
        {
            private readonly int callSite;
            private readonly Guid guard;

            public GuardKey(int callSite, Guid guard)
            {
                this.callSite = callSite;
                this.guard = guard;
            }

            public bool Equals(GuardKey other) => callSite == other.callSite && guard.Equals(other.guard);

            public override bool Equals(object obj) => obj is GuardKey other && Equals(other);

            public override int GetHashCode() => unchecked((callSite * 397) ^ guard.GetHashCode());
        }
    }
}

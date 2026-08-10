using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One breakpoint firing: which breakpoint, on which agent, at which moment.
    ///
    /// <para>
    /// Carries the recording rather than a copy of what happened, because everything the editor does with a
    /// hit needs it: the scrubber has to be handed the same recording the tick belongs to (a tick number means
    /// nothing outside its own recording — the rule <see cref="BehaviorTreeDebugSession.TickFor"/> exists to
    /// enforce), and the why-inspector explains from it.
    /// </para>
    /// </summary>
    public readonly struct BehaviorTreeBreakpointHit
    {
        public BehaviorTreeBreakpointHit(
            BehaviorTreeBreakpoint breakpoint,
            IBehaviorTreeRecording recording,
            string agentName,
            in BehaviorTreeEvent cause)
        {
            Breakpoint = breakpoint;
            Recording = recording;
            AgentName = agentName;
            Cause = cause;
        }

        public BehaviorTreeBreakpoint Breakpoint { get; }

        /// <summary>The agent's recording, so the tick can be scrubbed to and the moment explained.</summary>
        public IBehaviorTreeRecording Recording { get; }

        public string AgentName { get; }

        /// <summary>
        /// The event that tripped it, whole. Everything the editor needs is on it — tick, sequence, call site,
        /// and the guid to select — and passing it through rather than unpacking it means a new breakpoint
        /// kind does not need new fields here.
        /// </summary>
        public BehaviorTreeEvent Cause { get; }

        public int Tick => Cause.Tick;

        /// <summary>Which running copy of the branch hit it. The breakpoint itself matches every call site.</summary>
        public int CallSiteId => Cause.CallSiteId;

        /// <summary>
        /// The node to select on the canvas.
        ///
        /// <para>
        /// Not always <see cref="BehaviorTreeEvent.NodeGuid"/>, which is why this is here rather than at the
        /// call site: a <see cref="BehaviorTreeEventKind.VariableWrite"/> puts the writing node in
        /// <c>RelatedGuid</c> and leaves <c>NodeGuid</c> empty, so selecting the subject blindly would select
        /// nothing on exactly the breakpoint whose whole purpose is "who wrote this".
        /// </para>
        /// </summary>
        public Guid SubjectGuid =>
            Cause.Kind == BehaviorTreeEventKind.VariableWrite ? Cause.RelatedGuid : Cause.NodeGuid;

        /// <summary>One line saying what happened, for the console and the panel.</summary>
        public string Describe()
        {
            var where = string.IsNullOrEmpty(AgentName) ? "an agent" : AgentName;

            var what = Cause.Kind switch
            {
                BehaviorTreeEventKind.NodeEnter => "entered",
                BehaviorTreeEventKind.NodeExit => $"exited {Cause.Status}",
                BehaviorTreeEventKind.NodeAborted => "was aborted",
                BehaviorTreeEventKind.NodeSkipped => "was skipped",
                BehaviorTreeEventKind.GuardEval => $"turned {(Cause.Flag ? "true" : "false")}",
                BehaviorTreeEventKind.VariableWrite => $"was written {Cause.OldValue} → {Cause.NewValue}",
                _ => Cause.Kind.ToString(),
            };

            return $"BH3 breakpoint — {Breakpoint.Describe()}: {what} on {where} at tick {Tick}.";
        }
    }
}

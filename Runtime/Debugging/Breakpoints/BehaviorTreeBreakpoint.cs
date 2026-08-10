using System;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// One armed breakpoint: what to watch, and enough about it for a panel to list it and for a file to
    /// remember it.
    ///
    /// <para>
    /// <b>Keyed by guid, and deliberately not by call site.</b> Finding 1 established that a node guid is not
    /// unique within one agent — a shared branch instantiated at two call sites keeps the original guids — and
    /// every other part of the debugger therefore addresses things as <c>(CallSiteId, NodeGuid)</c>. A
    /// breakpoint is the one thing that should not: the spec asks for it to live "on the asset-side debug
    /// config, not the instance", and that is also what a designer means. Right-clicking a node in Attack and
    /// choosing Break on Enter is a statement about that node, not about the copy of Attack that Combat
    /// happens to be running. So a breakpoint matches every call site, and
    /// <see cref="BehaviorTreeBreakpointHit.CallSiteId"/> reports which one actually hit.
    /// </para>
    ///
    /// <para>
    /// One breakpoint per target, never two. A second "break on exit" for a node already broken on entry edits
    /// <see cref="Events"/> rather than adding a row, which is what keeps the panel a list of nodes instead of
    /// a list of events and keeps the store's lookup a single dictionary hit on the hot path.
    /// </para>
    /// </summary>
    public sealed class BehaviorTreeBreakpoint
    {
        private BehaviorTreeBreakpoint(BehaviorTreeBreakpointKind kind, Guid targetGuid, string variableKey)
        {
            Kind = kind;
            TargetGuid = targetGuid;
            VariableKey = variableKey;
            Enabled = true;
        }

        /// <summary>A node breakpoint, firing on the moments named in <paramref name="events"/>.</summary>
        public static BehaviorTreeBreakpoint ForNode(Guid node, BehaviorTreeNodeBreakEvents events)
        {
            return new BehaviorTreeBreakpoint(BehaviorTreeBreakpointKind.Node, node, null) { Events = events };
        }

        /// <summary>A guard breakpoint, firing when the guard changes its mind in the named direction.</summary>
        public static BehaviorTreeBreakpoint ForGuard(Guid guard, BehaviorTreeGuardBreakOn breakOn)
        {
            return new BehaviorTreeBreakpoint(BehaviorTreeBreakpointKind.Guard, guard, null) { GuardBreakOn = breakOn };
        }

        /// <summary>
        /// A variable breakpoint. <paramref name="expectedValue"/> null or empty means any write; otherwise
        /// only a write whose new value matches — see <see cref="ExpectedValue"/> for what "matches" means.
        /// </summary>
        public static BehaviorTreeBreakpoint ForVariable(string key, string expectedValue = null)
        {
            return new BehaviorTreeBreakpoint(BehaviorTreeBreakpointKind.Variable, Guid.Empty, key)
            {
                ExpectedValue = string.IsNullOrEmpty(expectedValue) ? null : expectedValue,
            };
        }

        public BehaviorTreeBreakpointKind Kind { get; }

        /// <summary>The node or the guard. <see cref="Guid.Empty"/> on a variable breakpoint.</summary>
        public Guid TargetGuid { get; }

        /// <summary>The variable name. Null on a node or guard breakpoint.</summary>
        public string VariableKey { get; }

        /// <summary>Which moments fire, on a node breakpoint.</summary>
        public BehaviorTreeNodeBreakEvents Events { get; internal set; } = BehaviorTreeNodeBreakEvents.None;

        /// <summary>Which direction fires, on a guard breakpoint.</summary>
        public BehaviorTreeGuardBreakOn GuardBreakOn { get; internal set; } = BehaviorTreeGuardBreakOn.EitherWay;

        /// <summary>
        /// The value a write must land on, or null for any write.
        ///
        /// <para>
        /// Compared against <see cref="BehaviorTreeEvent.NewValue"/>, which is
        /// <see cref="BehaviorTreeEvent.Describe"/>'s output: <c>ToString()</c> capped at 64 characters. So
        /// this matches what the recording says the value was, not the object itself — which is the honest
        /// contract, since the recorder never keeps the object. For primitives, strings and vectors that is
        /// exactly what a designer typed; for a <c>List&lt;T&gt;</c> it is a truncated type name and matching
        /// on it is not useful. Ordinal, case-sensitive, because "True" and "true" are different renderings
        /// and silently conflating them would make a breakpoint fire on a value the reader did not ask for.
        /// </para>
        /// </summary>
        public string ExpectedValue { get; internal set; }

        /// <summary>
        /// Whether this fires. Disabling rather than deleting is the point of the panel's checkbox: an
        /// argument for keeping a breakpoint you are not currently using is that re-finding the node is the
        /// expensive part.
        /// </summary>
        public bool Enabled { get; internal set; }

        /// <summary>
        /// What to call this in the panel and in the log line, resolved when it was armed. Names live on the
        /// graph and the store is pure C# with no graph to ask, so the alternative to remembering it is a
        /// panel that lists four guids. Purely cosmetic — nothing matches on it.
        /// </summary>
        public string Label { get; internal set; }

        /// <summary>
        /// Which tree it was armed from, for the panel's tooltip. Cosmetic, like <see cref="Label"/>, and
        /// nothing matches on it — a breakpoint is keyed by node guid alone, so a node in a shared branch
        /// fires wherever that branch runs.
        /// </summary>
        public string TreeName { get; internal set; }

        /// <summary>
        /// How many times this has fired since the counters were last reset. Not persisted — a hit count from
        /// a previous session is a statement about a run that no longer exists.
        /// </summary>
        public int HitCount { get; internal set; }

        /// <summary>Whether this breakpoint should fire for a node event of the given kind.</summary>
        public bool Matches(BehaviorTreeNodeBreakEvents moment)
        {
            return Kind == BehaviorTreeBreakpointKind.Node && (Events & moment) != 0;
        }

        /// <summary>Whether this breakpoint should fire for a guard that just became <paramref name="result"/>.</summary>
        public bool MatchesGuard(bool result)
        {
            if (Kind != BehaviorTreeBreakpointKind.Guard) return false;

            return GuardBreakOn switch
            {
                BehaviorTreeGuardBreakOn.BecameTrue => result,
                BehaviorTreeGuardBreakOn.BecameFalse => !result,
                _ => true,
            };
        }

        /// <summary>Whether this breakpoint should fire for a write landing on <paramref name="newValue"/>.</summary>
        public bool MatchesWrite(string newValue)
        {
            if (Kind != BehaviorTreeBreakpointKind.Variable) return false;

            return ExpectedValue == null || string.Equals(ExpectedValue, newValue, StringComparison.Ordinal);
        }

        /// <summary>
        /// A short description of what this is watching, for the panel and the log line. Says nothing about
        /// whether it has fired — that is the caller's to add, since a hit knows the tick and this does not.
        /// </summary>
        public string Describe()
        {
            var subject = string.IsNullOrEmpty(Label)
                ? Kind == BehaviorTreeBreakpointKind.Variable ? VariableKey : TargetGuid.ToString()
                : Label;

            return Kind switch
            {
                BehaviorTreeBreakpointKind.Node => $"{subject} [{Events}]",
                BehaviorTreeBreakpointKind.Guard => $"{subject} [{GuardBreakOn}]",
                _ => ExpectedValue == null ? $"{subject} [any write]" : $"{subject} == {ExpectedValue}",
            };
        }
    }
}

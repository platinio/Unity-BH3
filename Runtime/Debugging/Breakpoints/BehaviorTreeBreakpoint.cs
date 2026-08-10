using System;
using System.Globalization;

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
        /// A variable breakpoint. An empty <paramref name="expectedValue"/> forces
        /// <see cref="BehaviorTreeVariableCompare.Changed"/>, since every other operator needs something to
        /// compare against and an operator with no operand is a breakpoint that can only confuse.
        /// </summary>
        public static BehaviorTreeBreakpoint ForVariable(
            string key,
            string expectedValue = null,
            BehaviorTreeVariableCompare compare = BehaviorTreeVariableCompare.Equals)
        {
            var value = string.IsNullOrEmpty(expectedValue) ? null : expectedValue;

            return new BehaviorTreeBreakpoint(BehaviorTreeBreakpointKind.Variable, Guid.Empty, key)
            {
                ExpectedValue = value,
                Compare = value == null ? BehaviorTreeVariableCompare.Changed : compare,
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
        /// The text the write is tested against, or null for any write.
        ///
        /// <para>
        /// Kept as text because that is what a designer types, and interpreted according to
        /// <see cref="Compare"/> and the type of the value actually written — see
        /// <see cref="MatchesWrite"/>. Numbers are compared numerically against the live value rather than
        /// against its rendering, so <c>ammo &lt; 5</c> works and <c>3.5</c> is not defeated by a machine whose
        /// culture renders it "3,5".
        /// </para>
        /// </summary>
        public string ExpectedValue { get; internal set; }

        /// <summary>How <see cref="ExpectedValue"/> is tested. Only meaningful on a variable breakpoint.</summary>
        public BehaviorTreeVariableCompare Compare { get; internal set; } = BehaviorTreeVariableCompare.Changed;

        /// <summary>
        /// Which matching occurrence actually stops the editor, 1-based. 1 means the first.
        ///
        /// <para>
        /// The answer to an oscillating guard, which is the classic behaviour tree bug and the one this whole
        /// debugger was specified to catch: a branch that enters and aborts every few ticks produces dozens of
        /// identical hits, and the interesting one is rarely the first. Setting this to 30 gets you there
        /// without pressing Play twenty-nine times. Every code debugger has this beside its conditional
        /// breakpoints for the same reason; it applies to all three kinds here, not only to variables.
        /// </para>
        /// </summary>
        public int BreakOnHit { get; internal set; } = 1;

        /// <summary>
        /// Why this has not been firing, when the reason is knowable. Null when there is nothing to say.
        ///
        /// <para>
        /// Set when an ordering operator meets a value that is not a number — the one way a well-formed
        /// breakpoint can be silently inert. A tool that exists to replace guessing must not itself require
        /// guessing about why it said nothing, so the panel shows this on the row.
        /// </para>
        /// </summary>
        public string Diagnostic { get; internal set; }

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

        /// <summary>
        /// How many times the condition has held, whether or not it fired. Differs from
        /// <see cref="HitCount"/> only while <see cref="BreakOnHit"/> is still being counted up to, and that
        /// gap is the whole point of showing both: "matched 12, fired 0" says the breakpoint is working and
        /// you asked to skip past this, where a bare "0" would look like it is broken.
        /// </summary>
        public int MatchCount { get; internal set; }

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

        /// <summary>
        /// Whether this breakpoint should fire for a write.
        ///
        /// <para>
        /// Takes both the live object and its rendering, and prefers the object. That is what makes the
        /// numeric operators possible at all, and it costs nothing: the value arrives at
        /// <c>GameplayNode.SaveVariable</c> already typed as <c>object</c>, so it is boxed either way, and it
        /// is passed straight through to here without the event ring ever holding a reference to it. The ring
        /// must keep strings — a reference kept there would go on mutating after the event was recorded, and
        /// the export would then show the value at export time rather than at write time — but a breakpoint is
        /// evaluated synchronously at the write, where the real value is still in hand.
        /// </para>
        ///
        /// <para>
        /// Three ladders, in order. Numbers compare numerically, which is what fixes both a culture that
        /// renders 3.5 as "3,5" and the impossibility of ordering rendered text. Booleans parse
        /// case-insensitively, so a designer typing <c>true</c> matches a value whose <c>ToString()</c> is
        /// "True" — that was a silent never-fires. Everything else falls back to the rendering, which is what
        /// the panel shows and therefore what someone is copying from.
        /// </para>
        /// </summary>
        public bool MatchesWrite(object value, string rendered)
        {
            if (Kind != BehaviorTreeBreakpointKind.Variable) return false;
            if (Compare == BehaviorTreeVariableCompare.Changed || ExpectedValue == null) return true;

            if (IsOrdering(Compare)) return MatchesOrdering(value, rendered);

            if (TryAsNumber(value, rendered, out var number) && TryParseNumber(ExpectedValue, out var expected))
            {
                return Compare switch
                {
                    BehaviorTreeVariableCompare.Equals => number.Equals(expected),
                    BehaviorTreeVariableCompare.NotEquals => !number.Equals(expected),
                    _ => ContainsText(rendered),
                };
            }

            if (value is bool actualBool && bool.TryParse(ExpectedValue, out var expectedBool))
            {
                return Compare switch
                {
                    BehaviorTreeVariableCompare.Equals => actualBool == expectedBool,
                    BehaviorTreeVariableCompare.NotEquals => actualBool != expectedBool,
                    _ => ContainsText(rendered),
                };
            }

            return Compare switch
            {
                BehaviorTreeVariableCompare.Equals => string.Equals(ExpectedValue, rendered, StringComparison.Ordinal),
                BehaviorTreeVariableCompare.NotEquals => !string.Equals(ExpectedValue, rendered, StringComparison.Ordinal),
                _ => ContainsText(rendered),
            };
        }

        /// <summary>
        /// The ordering operators, which are numeric only. A value that is not a number does not match and
        /// leaves <see cref="Diagnostic"/> explaining why, rather than making this look like a breakpoint the
        /// program never reached.
        /// </summary>
        private bool MatchesOrdering(object value, string rendered)
        {
            if (!TryParseNumber(ExpectedValue, out var expected))
            {
                Diagnostic ??= $"'{ExpectedValue}' is not a number, so {Compare} can never match.";
                return false;
            }

            if (!TryAsNumber(value, rendered, out var number))
            {
                Diagnostic ??= $"{VariableKey} held {rendered}, which is not a number, so {Compare} cannot apply.";
                return false;
            }

            return Compare switch
            {
                BehaviorTreeVariableCompare.LessThan => number < expected,
                BehaviorTreeVariableCompare.LessOrEqual => number <= expected,
                BehaviorTreeVariableCompare.GreaterThan => number > expected,
                _ => number >= expected,
            };
        }

        private bool ContainsText(string rendered)
        {
            return rendered != null && ExpectedValue != null &&
                   rendered.IndexOf(ExpectedValue, StringComparison.Ordinal) >= 0;
        }

        public static bool IsOrdering(BehaviorTreeVariableCompare compare)
        {
            return compare is BehaviorTreeVariableCompare.LessThan
                or BehaviorTreeVariableCompare.LessOrEqual
                or BehaviorTreeVariableCompare.GreaterThan
                or BehaviorTreeVariableCompare.GreaterOrEqual;
        }

        /// <summary>
        /// The written value as a number, if it is one. Strings are parsed too, because a Visual Scripting
        /// variable is loosely typed and a value that reads as a number to the person watching it should
        /// behave like one.
        /// </summary>
        public static bool TryAsNumber(object value, string rendered, out double number)
        {
            switch (value)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return true;
                case string text:
                    return TryParseNumber(text, out number);
                case null:
                    return TryParseNumber(rendered, out number);
                default:
                    number = 0.0;
                    return false;
            }
        }

        /// <summary>
        /// Parses what the designer typed. Invariant first, then the editor's own culture, so both "3.5" and
        /// a comma-decimal machine's "3,5" work — the value in the watch panel is rendered in the current
        /// culture and is exactly what someone copies from.
        /// </summary>
        public static bool TryParseNumber(string text, out double number)
        {
            if (!string.IsNullOrEmpty(text))
            {
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return true;
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number)) return true;
            }

            number = 0.0;
            return false;
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

            var what = Kind switch
            {
                BehaviorTreeBreakpointKind.Node => $"{subject} [{Events}]",
                BehaviorTreeBreakpointKind.Guard => $"{subject} [{GuardBreakOn}]",
                _ => Compare == BehaviorTreeVariableCompare.Changed || ExpectedValue == null
                    ? $"{subject} [any write]"
                    : $"{subject} {Symbol(Compare)} {ExpectedValue}",
            };

            return BreakOnHit > 1 ? $"{what}, from hit #{BreakOnHit}" : what;
        }

        /// <summary>The operator as a designer would write it, for the row and the log line.</summary>
        public static string Symbol(BehaviorTreeVariableCompare compare)
        {
            return compare switch
            {
                BehaviorTreeVariableCompare.Equals => "==",
                BehaviorTreeVariableCompare.NotEquals => "!=",
                BehaviorTreeVariableCompare.LessThan => "<",
                BehaviorTreeVariableCompare.LessOrEqual => "<=",
                BehaviorTreeVariableCompare.GreaterThan => ">",
                BehaviorTreeVariableCompare.GreaterOrEqual => ">=",
                BehaviorTreeVariableCompare.Contains => "contains",
                _ => "changed",
            };
        }
    }
}

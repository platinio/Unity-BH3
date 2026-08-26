using System;
using System.Collections.Generic;
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
        /// The tallies, one per agent that has reached this breakpoint.
        ///
        /// <para>
        /// <b>Counted per agent, because "the 30th time" means the 30th time <em>this</em> agent got there.</b>
        /// A breakpoint deliberately matches every agent running the tree — that is what
        /// <see cref="BehaviorTreeBreakpointHit.CallSiteId"/> exists to report — so one shared counter would
        /// let forty zombies race each other to <see cref="BreakOnHit"/> and stop the editor on whichever
        /// arrived first, which is rarely the one being debugged. It also survives changing which agent that
        /// is: the count belonging to the zombie you were watching stays with that zombie instead of
        /// becoming a head start for the next one.
        /// </para>
        ///
        /// <para>
        /// Keyed by the recording rather than by agent name, which is not unique, and dropped when the
        /// recorder unregisters — a dead agent's tally describes a run that is over, and holding its
        /// recording as a key would keep the whole event ring alive for the rest of the session.
        /// </para>
        /// </summary>
        private readonly Dictionary<object, Tally> tallies = new();

        /// <summary>
        /// Where a match with no recording is filed. Nothing in the shipped paths passes null — the recorder
        /// hands itself in — but a count that silently went nowhere would be worse than one filed here.
        /// </summary>
        private static readonly object Unattributed = new();

        /// <summary>
        /// How many times this has fired across every agent since the counters were last reset. Not
        /// persisted — a hit count from a previous session is a statement about a run that no longer exists.
        /// <see cref="BreakOnHit"/> is measured against <see cref="HitsFor"/>, not against this.
        /// </summary>
        public int HitCount
        {
            get
            {
                int total = 0;

                foreach (var tally in tallies.Values)
                {
                    total += tally.Hits;
                }

                return total;
            }
        }

        /// <summary>
        /// How many times the condition has held across every agent, whether or not it fired. Differs from
        /// <see cref="HitCount"/> only while <see cref="BreakOnHit"/> is still being counted up to, and that
        /// gap is the whole point of showing both: "matched 12, fired 0" says the breakpoint is working and
        /// you asked to skip past this, where a bare "0" would look like it is broken.
        /// </summary>
        public int MatchCount
        {
            get
            {
                int total = 0;

                foreach (var tally in tallies.Values)
                {
                    total += tally.Matches;
                }

                return total;
            }
        }

        /// <summary>How many agents have reached this breakpoint since the last reset.</summary>
        public int AgentsMatched => tallies.Count;

        /// <summary>
        /// The closest any single agent has come to <see cref="BreakOnHit"/>. What "how near am I" means
        /// when several agents are counting separately and no one of them speaks for the row.
        /// </summary>
        public int PeakMatches
        {
            get
            {
                int peak = 0;

                foreach (var tally in tallies.Values)
                {
                    if (tally.Matches > peak) peak = tally.Matches;
                }

                return peak;
            }
        }

        /// <summary>How many times this has fired for one agent — the count that decides when the editor stops.</summary>
        public int HitsFor(IBehaviorTreeRecording recording) =>
            tallies.TryGetValue((object)recording ?? Unattributed, out var tally) ? tally.Hits : 0;

        /// <summary>How many times the condition has held for one agent, fired or not.</summary>
        public int MatchesFor(IBehaviorTreeRecording recording) =>
            tallies.TryGetValue((object)recording ?? Unattributed, out var tally) ? tally.Matches : 0;

        /// <summary>This agent's tally, created the first time it reaches here. The matcher's write path.</summary>
        internal Tally TallyFor(IBehaviorTreeRecording recording)
        {
            var key = (object)recording ?? Unattributed;

            if (!tallies.TryGetValue(key, out var tally)) tallies[key] = tally = new Tally();

            return tally;
        }

        /// <summary>Drops one agent's tally, when its recorder goes away.</summary>
        internal void Forget(IBehaviorTreeRecording recording)
        {
            if (recording != null) tallies.Remove(recording);
        }

        /// <summary>Drops every tally.</summary>
        internal void ResetTallies() => tallies.Clear();

        /// <summary>One agent's counts. A class rather than a struct so the matcher can bump it in place.</summary>
        internal sealed class Tally
        {
            internal int Matches;
            internal int Hits;
        }

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

            // Contains on a number is legal and almost never meant. It matches the rendered text, so "1" also
            // matches 10, 21 and 100 — a breakpoint that fires more often than the reader expects rather than
            // less, which is the harder kind to notice. Said once, and matching carries on unchanged: unlike
            // ordering, there is a defined answer here and refusing to give it would be the bigger surprise.
            if (Compare == BehaviorTreeVariableCompare.Contains && TryAsNumber(value, rendered, out _))
            {
                Diagnostic ??=
                    $"{VariableKey} is a number, and 'contains' matches its text — \"{ExpectedValue}\" also " +
                    "matches any number containing it. Use == to test a value.";
            }

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

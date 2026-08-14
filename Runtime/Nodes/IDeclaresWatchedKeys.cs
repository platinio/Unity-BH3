using System.Collections.Generic;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// A node that can state which agent facts its value depends on.
    ///
    /// <para>
    /// This is the seam that lets a <see cref="ReactiveGuard"/> learn its schedule from its condition instead
    /// of being told it by hand. A guard reads no variables of its own — it pulls a value off a port — so
    /// every key in its <see cref="GuardTrigger.Keys"/> list is really a <em>claim</em> about what the thing
    /// feeding that port reads. Until something could answer that question, the claim could only be typed by a
    /// human and checked by nobody, and a wrong one is silent: the guard simply never wakes.
    /// </para>
    ///
    /// <para>
    /// <b>Declared, not derived.</b> An implementer returns what it <em>promises</em> to read, not what a walk
    /// of its innards happened to find. Derivation exists too — <c>FunctionGraphAsset.DeriveReadKeys()</c> —
    /// but its job is to keep a declaration honest, not to replace it: a key computed at runtime is invisible
    /// to any walk, and a declaration is the only place that dependency can be stated at all.
    /// </para>
    ///
    /// <para>
    /// Expressed as a capability rather than a type test, matching the rule
    /// <see cref="ConditionalExecution.Triggers"/> already states: everything that walks guards filters on
    /// capability, never on type. The second implementer is already named — spec 10's open question 5 is an
    /// attribute letting a C# value node declare its keys the way a Function does, and it lands here without
    /// touching the walk.
    /// </para>
    /// </summary>
    public interface IDeclaresWatchedKeys
    {
        /// <summary>
        /// The agent facts this node's value depends on. Empty when it has none to declare, which is the
        /// honest answer for a node reading nothing agent-scoped — never a reason to guess.
        /// </summary>
        IReadOnlyList<string> DeclaredWatchedKeys { get; }
    }
}

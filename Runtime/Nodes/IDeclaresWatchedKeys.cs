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
    /// <b>An interface, and therefore a type test at the call site — deliberately.</b> The obvious
    /// alternative is a virtual on <see cref="BehaviorTreeNode"/> with an empty default, which would remove
    /// the test everywhere. It is rejected because this concept is not universal: only a node that produces
    /// a value from agent state has an answer, and <c>Selector.DeclaredWatchedKeys</c> returning empty is
    /// noise rather than a default. A member every node carries should mean something for every node.
    /// </para>
    ///
    /// <para>
    /// That is the line worth holding, and it is the opposite of where node problems land: any node can be
    /// broken, for any reason, so a problem is asked of the base type and contributed to from several
    /// sources. The two look alike at the call site and are not the same shape.
    /// </para>
    ///
    /// <para>
    /// <see cref="ConditionalExecution.Triggers"/> is not a counter-example. Its rule — filter on capability,
    /// never on type — is stated for a <em>family</em>, and it puts the member on the guard base where every
    /// member of that family has an answer. There is no equivalent family here: value-producing nodes are
    /// spread across <c>GameplayNode</c> and <c>BaseVisualScriptingNode</c>, so the only uniform home would
    /// be the root.
    /// </para>
    ///
    /// <para>
    /// The second implementer is already named — spec 10's open question 5 is a plain C# value node
    /// declaring its keys the way a Function does. It lands by implementing this, without touching the walk,
    /// and that is exactly the case a <c>BaseVisualScriptingNode</c>-scoped member would have excluded.
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

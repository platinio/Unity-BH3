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

        /// <summary>
        /// Facts this node's source is known to read <em>without</em> declaring them — where the declaration
        /// above is provably incomplete. Empty when it is complete, and equally empty when the node has no
        /// way to tell, which is the honest answer rather than a guess.
        /// </summary>
        ///
        /// <para>
        /// The counterpart of the declaration, and on the same interface because it is the same subject: what
        /// this node knows about the facts its value depends on. A node that can answer the first question can
        /// usually say something about the second, and no node that cannot answer the first has an opinion on
        /// either.
        /// </para>
        ///
        /// <para>
        /// It exists because the gap is otherwise silent and worse than a stale trigger. Inheritance hands a
        /// guard the <em>declaration</em>, so a fact the source reads but never declared does not schedule
        /// anything: the guard keeps its old schedule, never wakes on that fact, and the branch stops firing
        /// with nothing to point at. Refreshing the guard's keys cannot fix it either, since that copies the
        /// declaration that is missing the key — the repair is on the source.
        /// </para>
        ///
        /// <para>
        /// Derivation is incomplete by construction, so this is a one-way test: what it finds is genuinely
        /// undeclared, and finding nothing does not prove the declaration is complete. A <c>GetVariable</c>
        /// whose name is computed at runtime is invisible to any walk.
        /// </para>
        IReadOnlyList<string> UndeclaredReadKeys { get; }
    }
}

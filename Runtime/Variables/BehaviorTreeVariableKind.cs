using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Which store a behavior tree reads a variable from, or writes one to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately not <see cref="Unity.VisualScripting.VariableKind"/>.</b> That enum is written for a
    /// flow graph and leads with <c>Flow</c> — per-invocation scratch that exists only while a
    /// <c>Unity.VisualScripting.Flow</c> is on the stack. A behavior tree has no such thing; a node runs
    /// across frames and its scratch belongs to <see cref="Graph"/>. Because <c>Flow</c> is that enum's zero
    /// value it was also the value every freshly dropped node started on, so the one kind the tree cannot
    /// serve was the one it offered by default: <c>Get Variable</c> answered null without saying why and
    /// <c>Set Variable</c> logged an error at runtime, both a frame at a time, both far from the node that
    /// caused it.
    /// </para>
    /// <para>
    /// Owning the enum makes that state unrepresentable. Every member here is a store a tree can actually
    /// reach, and the zero value is <see cref="None"/> — not a sixth store but the absence of a choice,
    /// which the variable nodes report on the canvas before anything runs. That is the same bargain
    /// <see cref="VariableKeyPort"/> strikes for the Key port: a default that cannot silently do the wrong
    /// thing, and a loud one that names the node.
    /// </para>
    /// <para>
    /// Member names match Unity's, which is what makes the change free for existing assets: Visual
    /// Scripting serializes an enum by name, so every <c>"Graph"</c> and <c>"Object"</c> already on disk
    /// deserializes into this type untouched. <c>"Flow"</c> is the one name with nowhere to land, and
    /// <see cref="RenamedFromAttribute"/> steers it to <see cref="None"/> so those nodes surface as the
    /// misconfiguration they always were rather than failing to load.
    /// </para>
    /// <para>
    /// The Visual Scripting units <c>GetBehaviorTreeVariable</c> and <c>SetBehaviorTreeVariable</c> use it
    /// too. They run inside a real flow graph, where <c>Flow</c> exists, but flow scratch is not tree state:
    /// nothing in a tree can read it and the recorder had to drop every such write, so a unit named after
    /// the tree was offering a store that only existed to be thrown away. Unity's own Get and Set Variable
    /// keep serving per-flow scratch. On those units <see cref="Graph"/> is the <em>script graph's</em>
    /// own variables rather than a branch scope, because a script graph has no branch; the tree nodes are
    /// the ones that reach the scope chain.
    /// </para>
    /// </remarks>
    public enum BehaviorTreeVariableKind
    {
        /// <summary>
        /// No store chosen. The zero value, so an unconfigured node reports a problem instead of guessing,
        /// and what the flight recorder stamps on every event that is not a variable write.
        /// </summary>
        [RenamedFrom("Flow")]
        None,

        /// <summary>
        /// Variables scoped to the running tree, through the calling chain: a node inside a branch sees
        /// that branch's values first and its caller's underneath, out to the agent. A branch's writes stay
        /// in the branch, which is what makes one safe to reuse across unrelated agents.
        /// </summary>
        Graph,

        /// <summary>
        /// Variables on the agent's own <c>Variables</c> component — the facts every branch on that agent
        /// can see, and the ones guards watch for changes.
        /// </summary>
        Object,

        /// <summary>Variables shared by everything in the active scene.</summary>
        Scene,

        /// <summary>Variables shared across scenes, reset when the application quits.</summary>
        Application,

        /// <summary>Variables that outlive the application. Unity object references are not supported.</summary>
        Saved
    }
}

using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Everything a node's body is allowed to know about the agent it is running on.
    ///
    /// <para>
    /// A node body written against this type reads per-agent state through the context rather than through
    /// <c>this</c>. That is the whole point, and it is worth being precise about why, because today the two
    /// are the same thing: <see cref="BehaviorTreeMachine"/> clones the tree per agent, so a node instance
    /// already belongs to exactly one agent and its fields are already private to it.
    /// </para>
    ///
    /// <para>
    /// The clone is what BH3 intends to stop paying for. Once one immutable tree is shared by every agent
    /// running it, a node instance no longer belongs to anyone and its fields become state that two hundred
    /// agents overwrite in turn — so every node body that reaches through <c>this</c> has to be rewritten,
    /// and every node body that reaches through a context does not. The members below are the ones that will
    /// still mean something on that day; a node's own fields are the ones that will not. Nothing here changes
    /// what a node can do, only where it asks.
    /// </para>
    ///
    /// <para>
    /// A <c>readonly struct</c> wrapping the node, so passing one costs nothing and allocates nothing on a
    /// path taken by every node on every tick. It is deliberately not a window onto the node itself: the
    /// narrow surface is the contract, and anything reachable only through the node is something the shared-
    /// tree refactor would have to take away again.
    /// </para>
    ///
    /// <para>
    /// <see cref="Memory{T}"/> is where a node keeps the state it used to keep in fields. Spec 07 writes it
    /// as <c>ctx.Memory&lt;T&gt;(this)</c>; it takes no node here because the context already knows which
    /// node it belongs to, which removes the one way that call could be got wrong.
    /// </para>
    /// </summary>
    public readonly struct BTContext
    {
        private readonly BehaviorTreeNode node;

        /// <summary>
        /// The agent's instance when this context is backed by a shared tree, null when it is backed by a
        /// cloned node. <b>This one field is the whole of the flip.</b>
        /// <para>
        /// A node body never asks which it is. It reads <see cref="Memory{T}"/>, <see cref="gameObject"/>
        /// and <see cref="TickChild"/>, and each of those answers from the instance when there is one and
        /// from the node when there is not — so the same body runs under
        /// <see cref="BehaviorTreeMachine"/> (clone per agent) and under
        /// <see cref="Instancing.SharedBehaviorTreeMachine"/> (one shared tree) with no recompilation and
        /// no branch of its own.
        /// </para>
        /// </summary>
        private readonly Instancing.TreeInstance instance;

        private readonly int index;

        /// <summary>Backed by a cloned node: state lives on the node, children come from its child list.</summary>
        internal BTContext(BehaviorTreeNode node)
        {
            this.node = node;
            instance = null;
            index = -1;
        }

        /// <summary>Backed by a shared tree: state lives in the agent's arrays, children come from the plan.</summary>
        internal BTContext(Instancing.TreeInstance instance, int index)
        {
            node = instance.Plan.Nodes[index];
            this.instance = instance;
            this.index = index;
        }

        /// <summary>Whether this context is backed by a shared tree rather than a per-agent clone.</summary>
        public bool IsShared => instance != null;

        /// <summary>
        /// This node's own state on this agent, created on first use — a timer, a current-child index, a
        /// component resolved at entry. <b>Everything a node body would otherwise have kept in a field
        /// belongs here.</b>
        ///
        /// <para>
        /// Declare one small class per node holding every field it needs, and read it at the top of each
        /// body:
        /// <code>
        /// private sealed class Memory { public float Elapsed; }
        ///
        /// public override void OnEnter(BTContext ctx) => ctx.Memory&lt;Memory&gt;().Elapsed = 0f;
        /// </code>
        /// </para>
        ///
        /// <para>
        /// A field on the node would work today and stop working the day one tree is shared by every agent
        /// running it, because the field every agent then writes is the same field. This is the same storage
        /// with somewhere to move to.
        /// </para>
        /// </summary>
        public T Memory<T>() where T : class, new() =>
            instance != null ? instance.MemoryAt<T>(index) : node.MemorySlot<T>();

        /// <summary>The machine running this tree, or null when the tree is ticked without one (edit-mode tests).</summary>
        public BehaviorTreeMachine Machine => instance != null ? null : node.BehaviorTreeMachine;

        /// <summary>The agent this node is running on.</summary>
        public GameObject gameObject => instance != null ? instance.Agent : node.gameObject;

        /// <inheritdoc cref="gameObject"/>
        public Transform transform => gameObject.transform;

        /// <summary>
        /// How many children this node has. From the baked plan on a shared tree, from the node's own
        /// child list on a clone — a composite body asks here and never learns which.
        /// </summary>
        public int ChildCount =>
            instance != null
                ? instance.Plan.ChildCount(index)
                : node is ContainerNode container ? container.GetChildren().Count : 0;

        /// <summary>
        /// Ticks a child by ordinal, entering it first if it is not already running — the universal form of
        /// <c>ContainerNode.TickChild</c>, and the reason a migrated composite runs unmodified on both
        /// runtimes.
        /// <para>
        /// "Is it running" is the question that differs: on a clone it is the node's own flag, on a shared
        /// tree it is this agent's slot in the running array. Both are answered here so that no composite
        /// has to ask.
        /// </para>
        /// </summary>
        public ExecutionStatus TickChild(int ordinal)
        {
            if (instance != null)
            {
                return Instancing.SharedTreeRunner.TickNode(instance, instance.Plan.ChildIndex(index, ordinal));
            }

            var child = ((ContainerNode)node).GetChildren()[ordinal];
            if (!child.IsRunning) child.OnNodeEnter();

            return child.OnUpdateInternal();
        }

        /// <summary>Exits a child by ordinal if it is running. Safe to call for one that is not.</summary>
        public void ExitChild(int ordinal)
        {
            if (instance != null)
            {
                Instancing.SharedTreeRunner.ExitNode(instance, instance.Plan.ChildIndex(index, ordinal));
                return;
            }

            ((ContainerNode)node).GetChildren()[ordinal].OnNodeExit();
        }

        /// <summary>
        /// The variables this node can see — the sub-tree's own instance first, then outward. Not reachable
        /// from <see cref="Machine"/>: it is assigned per call site, so a node inside a sub-tree resolves
        /// against that sub-tree's scope rather than the root's.
        /// </summary>
        public BehaviorTreeVariableScope VariableScope => node.VariableScope;

        /// <summary>Where this node reports what it did, or null when nothing is recording.</summary>
        public BehaviorTreeFlightRecorder FlightRecorder => node.FlightRecorder;

        /// <summary>What an embedded Visual Scripting graph sees — the scope chain collapsed to a flat set.</summary>
        public VariableDeclarations ScriptGraphVariables => node.ScriptGraphVariables;

        /// <summary>
        /// One of this node's children, by ordinal. Needed by composites that ask a child a question before
        /// deciding to run it — the preemption scan, for one.
        /// </summary>
        public BehaviorTreeNode Child(int ordinal) =>
            instance != null
                ? instance.Plan.Nodes[instance.Plan.ChildIndex(index, ordinal)]
                : ((ContainerNode)node).GetChildren()[ordinal];

        /// <summary>Reads a port at the type it declares. Prefer this to touching the port directly.</summary>
        public T GetValue<T>(ValueInput port) => port.GetValue<T>();

        /// <summary>Reads a port whose declared type is <c>object</c>.</summary>
        public object GetValue(ValueInput port) => port.GetValue();

        /// <summary>A component on the agent.</summary>
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();

        /// <summary>
        /// The component a port points at, falling back to the agent's own.
        /// <para>
        /// Reimplemented here rather than delegated to the node, because the node's fallback reaches the
        /// agent through its machine — which a shared node does not have. The port half is shared structure
        /// and is read the same way on both paths.
        /// </para>
        /// </summary>
        public T GetComponent<T>(ValueInput port) where T : Component
        {
            var component = port.GetComponent<T>();

            return component != null ? component : gameObject.GetComponent<T>();
        }

        /// <summary>
        /// Resolves a component from a port and says so when it cannot find one. Call it when the node is
        /// entered — not at wake, not per tick; see <see cref="BehaviorTreeNode.TryResolve{T}"/>.
        /// </summary>
        /// <returns>False when nothing was found, in which case the node should fail rather than continue.</returns>
        public bool TryResolve<T>(ValueInput port, out T component) where T : Component
        {
            component = GetComponent<T>(port);

            if (component != null) return true;

            Debug.LogError(
                $"'{node.NodeName}' found no {typeof(T).Name} on its {port.key} or on the agent itself.",
                gameObject);

            return false;
        }

        /// <summary>The GameObject a blackboard variable names, falling back to the agent itself.</summary>
        public GameObject GetTargetGameObject(GameObjectBlackboardVariable gameObjectVariable) =>
            node.GetTargetGameObject(gameObjectVariable);
    }
}

using System.Diagnostics;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// The seam the running tree records through. Every method here is
    /// <see cref="ConditionalAttribute"/>-gated, so outside the editor and dev builds the compiler removes
    /// the <em>call sites</em> — not just the work, but the argument evaluation too. A tree shipped without
    /// the define does not check a flag, does not read a field, and does not know this class exists.
    ///
    /// <para>
    /// Two <c>Conditional</c> attributes mean "or", so recording is available in the editor with no project
    /// setup, and in a player build when <c>BH3_DEV_TOOLS</c> is defined. That define is deliberately not
    /// recorder-specific: the runtime asserts specs 02 and 04 want in
    /// <see cref="BehaviorTreeMachine"/>.<c>Awake</c> belong behind the same switch, and one dev-build gate
    /// is easier to reason about than three.
    /// </para>
    ///
    /// <para>
    /// The methods return void for the same reason — <c>Conditional</c> only applies to void methods. That is
    /// why the machine is handed its recorder through <see cref="Attach"/> rather than asking for one.
    /// </para>
    /// </summary>
    public static class BehaviorTreeRecorder
    {
        /// <summary>Scripting define that turns recording on in a player build.</summary>
        public const string DevToolsDefine = "BH3_DEV_TOOLS";

        private const string Editor = "UNITY_EDITOR";

        #region Lifetime

        /// <summary>
        /// Gives a machine a recorder and registers it. Called from <c>Awake</c> before the graph is walked,
        /// so scopes can be registered as they are built.
        /// </summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void Attach(BehaviorTreeMachine machine, string treeName)
        {
            if (machine == null) return;

            var recorder = new BehaviorTreeFlightRecorder(
                machine.gameObject != null ? machine.gameObject.name : "(agent)",
                treeName,
                BehaviorTreeFlightRecorders.DefaultCapacity,
                BehaviorTreeFlightRecorders.DefaultTraceCapacity);

            machine.SetFlightRecorder(recorder);
            BehaviorTreeFlightRecorders.Register(recorder);
        }

        /// <summary>
        /// Takes the recorder off a machine and out of the registry.
        ///
        /// <para>
        /// The machine's own reference is cleared as well as the registration, so "detached" and "still
        /// holding a recording" stop being possible at the same time. Unregistering alone left the
        /// recorder reachable through <c>machine.FlightRecorder</c> -- which is a plain auto-property and
        /// so keeps answering on a destroyed machine -- and every editor-side check for "is this agent
        /// recording" reads exactly that.
        /// </para>
        /// </summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void Detach(BehaviorTreeMachine machine)
        {
            if (machine == null) return;

            BehaviorTreeFlightRecorders.Unregister(machine.FlightRecorder);
            machine.SetFlightRecorder(null);
        }

        /// <summary>Hands every node in a graph the recorder to report to.</summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void Bind(BehaviorTreeGraph graph, BehaviorTreeFlightRecorder recorder)
        {
            if (graph == null) return;

            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }
        }

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void BeginTick(BehaviorTreeMachine machine)
        {
            machine?.FlightRecorder?.BeginTick();
        }

        #endregion

        #region Call sites

        /// <summary>Binds the root tree's scope to the root call site.</summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void BindRootScope(BehaviorTreeMachine machine, BehaviorTreeVariableScope scope)
        {
            machine?.FlightRecorder?.BindRootScope(scope);
        }

        /// <summary>Registers the scope a <see cref="RunBehaviorTreeGraphNode"/> opened around its branch.</summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void RegisterCallSite(
            RunBehaviorTreeGraphNode runNode,
            BehaviorTreeVariableScope scope,
            BehaviorTreeVariableScope parentScope)
        {
            var recorder = runNode?.FlightRecorder;
            if (recorder == null) return;

            recorder.RegisterCallSite(
                scope,
                parentScope,
                runNode.guid,
                runNode.BehaviorTreeGraphAsset != null ? runNode.BehaviorTreeGraphAsset.name : "(none assigned)");
        }

        #endregion

        #region Events

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void NodeEnter(BehaviorTreeNode node)
        {
            node?.FlightRecorder?.NodeEnter(node);
        }

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void NodeExit(BehaviorTreeNode node, ExecutionStatus status)
        {
            node?.FlightRecorder?.NodeExit(node, status);
        }

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void GuardEval(BehaviorTreeNode owner, ConditionalExecution guard, bool result)
        {
            owner?.FlightRecorder?.GuardEval(owner, guard, result);
        }

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void NodeSkipped(BehaviorTreeNode node, ConditionalExecution guard)
        {
            node?.FlightRecorder?.NodeSkipped(node, guard);
        }

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void NodeAborted(BehaviorTreeNode node, ConditionalExecution guard)
        {
            node?.FlightRecorder?.NodeAborted(node, guard);
        }

        /// <summary>A running branch gave way to a higher-priority one. See <see cref="BehaviorTreeEventKind.NodeTakenOver"/>.</summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void NodeTakenOver(
            BehaviorTreeNode victim, BehaviorTreeNode preemptor, ConditionalExecution guard)
        {
            victim?.FlightRecorder?.NodeTakenOver(victim, preemptor, guard);
        }

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void VariableWrite(
            BehaviorTreeNode writer,
            string key,
            BehaviorTreeVariableKind variableKind,
            object oldValue,
            object newValue)
        {
            writer?.FlightRecorder?.VariableWrite(writer, key, variableKind, oldValue, newValue);
        }

        /// <summary>
        /// The behavior tree node whose script graph is currently running, innermost first.
        ///
        /// <para>
        /// A Visual Scripting unit has no way back to the node that invoked its graph — a
        /// <see cref="GraphPointer"/>'s root is the script graph asset, and nothing links it to the tree. So
        /// the node announces itself for the duration of the call, which is what lets a write made inside a
        /// graph be attributed to a real node guid instead of a bare name.
        /// </para>
        ///
        /// <para>
        /// A stack rather than a field because a graph can run another graph. Safe as a static because graph
        /// evaluation is synchronous and single-threaded — the push and its pop are the same call.
        /// </para>
        /// </summary>
        private static readonly System.Collections.Generic.Stack<BehaviorTreeNode> scriptGraphOwners = new();

        /// <summary>Announces the node about to run a script graph. Always pair with <see cref="PopScriptGraphOwner"/>.</summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void PushScriptGraphOwner(BehaviorTreeNode node)
        {
            scriptGraphOwners.Push(node);
        }

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void PopScriptGraphOwner()
        {
            if (scriptGraphOwners.Count > 0) scriptGraphOwners.Pop();
        }

        /// <summary>
        /// A write made by a unit inside a Visual Scripting graph.
        ///
        /// <para>
        /// Attributed to the node that ran the graph when one announced itself, so the write carries a guid
        /// the why-inspector can point at. It falls back to a plain name when no node did — a graph run
        /// outside a tree still has a writer worth recording, just not a locatable one. The choice lives here
        /// rather than at the call site so that the whole decision compiles out with the rest of the facade.
        /// </para>
        /// </summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void ScriptGraphVariableWrite(
            BehaviorTreeMachine machine,
            string writerName,
            string key,
            Unity.VisualScripting.VariableKind variableKind,
            object oldValue,
            object newValue)
        {
            var kind = StoreOf(variableKind);

            // A flow variable is not tree state and never becomes any: no node can read it, no guard can
            // watch it, and the flow holding it is gone before anyone could look. Recording one puts a row
            // in the variable watch under a store that does not exist -- the same phantom the guard above
            // SaveVariable exists to prevent, arrived by a different route.
            if (kind == BehaviorTreeVariableKind.None) return;

            var owner = scriptGraphOwners.Count > 0 ? scriptGraphOwners.Peek() : null;

            if (owner != null)
            {
                owner.FlightRecorder?.VariableWrite(owner, key, kind, oldValue, newValue);
                return;
            }

            machine?.FlightRecorder?.ExternalVariableWrite(writerName, key, oldValue, newValue, kind);
        }

        /// <summary>
        /// The one place a Visual Scripting kind becomes a behavior tree one.
        /// </summary>
        /// <remarks>
        /// The caller above is a script graph unit, so its kind is Unity's — it runs in a flow, where
        /// <c>Flow</c> is a real store. The recording is BH3's, where it is not. Rather than push that
        /// mismatch onto the unit, the facade absorbs it here, for the same reason it already decides
        /// writer attribution here: everything in this class compiles out of a shipped build, and a
        /// translation the call site never performs is a translation that costs nothing to ship.
        /// <para>
        /// Flow scratch maps to <see cref="BehaviorTreeVariableKind.None"/>, and the caller drops it. The
        /// write happened, but there is no store to group it under and nothing in a tree can ever read it,
        /// so a recorded one is a row about a value no reader can reach. Keeping
        /// <see cref="BehaviorTreeVariableKind.None"/> meaning exactly "not a variable write" is worth more
        /// than an event only the flow that already ended could have used.
        /// </para>
        /// </remarks>
        private static BehaviorTreeVariableKind StoreOf(Unity.VisualScripting.VariableKind kind)
        {
            switch (kind)
            {
                case Unity.VisualScripting.VariableKind.Graph: return BehaviorTreeVariableKind.Graph;
                case Unity.VisualScripting.VariableKind.Object: return BehaviorTreeVariableKind.Object;
                case Unity.VisualScripting.VariableKind.Scene: return BehaviorTreeVariableKind.Scene;
                case Unity.VisualScripting.VariableKind.Application:
                    return BehaviorTreeVariableKind.Application;
                case Unity.VisualScripting.VariableKind.Saved: return BehaviorTreeVariableKind.Saved;
                default: return BehaviorTreeVariableKind.None;
            }
        }

        /// <summary>
        /// A write made by something outside the tree — a perception sensor publishing a fact.
        ///
        /// <para>
        /// Gated like everything else here, which is the reason a sensor should call this rather than reaching
        /// for the recorder itself: the call and its arguments vanish from a shipped build, so publishing a
        /// fact costs a sensor nothing outside the editor and dev builds.
        /// </para>
        /// </summary>
        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void ExternalVariableWrite(
            BehaviorTreeMachine machine,
            string writerName,
            string key,
            object oldValue,
            object newValue,
            BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.Object)
        {
            machine?.FlightRecorder?.ExternalVariableWrite(writerName, key, oldValue, newValue, variableKind);
        }

        #endregion
    }
}

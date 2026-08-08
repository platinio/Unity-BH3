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

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void Detach(BehaviorTreeMachine machine)
        {
            if (machine == null) return;

            BehaviorTreeFlightRecorders.Unregister(machine.FlightRecorder);
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

        [Conditional(Editor), Conditional(DevToolsDefine)]
        public static void VariableWrite(BehaviorTreeNode writer, string key, object oldValue, object newValue)
        {
            writer?.FlightRecorder?.VariableWrite(writer, key, oldValue, newValue);
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
            BehaviorTreeMachine machine, string writerName, string key, object oldValue, object newValue)
        {
            machine?.FlightRecorder?.ExternalVariableWrite(writerName, key, oldValue, newValue);
        }

        #endregion
    }
}

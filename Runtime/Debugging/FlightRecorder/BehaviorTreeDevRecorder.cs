using System.Collections.Generic;
using System.Diagnostics;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// What the running tree records through. Every method is <see cref="ConditionalAttribute"/>-gated, so
    /// outside the editor and dev builds the compiler removes the call sites themselves, argument evaluation
    /// included. Two Conditional attributes mean "or". Anything added here must return void, which is the
    /// only shape the attribute applies to.
    /// </summary>
    public static class BehaviorTreeDevRecorder
    {
        public const string DevToolsDefine = "BH3_DEV_TOOLS";

        private const string EditorDefine = "UNITY_EDITOR";

        /// <summary>
        /// The node whose script graph is running, innermost first. A Visual Scripting unit has no way back
        /// to the node that invoked its graph, so the node announces itself for the duration of the call. A
        /// stack rather than a field because a graph can run another graph; safe as a static because graph
        /// evaluation is synchronous and single-threaded.
        /// </summary>
        private static readonly Stack<BehaviorTreeNode> scriptGraphOwners = new();

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void Attach(BehaviorTreeMachine machine, string treeName)
        {
            if (machine == null) return;

            var recorder = new BehaviorTreeFlightRecorder(
                machine.gameObject != null ? machine.gameObject.name : BehaviorTreeRecordingNames.UnnamedAgent,
                treeName,
                BehaviorTreeFlightRecorderRegistry.DefaultCapacity,
                BehaviorTreeFlightRecorderRegistry.DefaultTraceCapacity);

            machine.SetFlightRecorder(recorder);
            BehaviorTreeFlightRecorderRegistry.Register(recorder);
        }

        /// <summary>
        /// The machine's own reference is cleared as well as the registration, so "detached" and "still
        /// holding a recording" stop being possible at the same time: every editor-side check for whether an
        /// agent is recording reads that reference, which keeps answering on a destroyed machine.
        /// </summary>
        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void Detach(BehaviorTreeMachine machine)
        {
            if (machine == null) return;

            BehaviorTreeFlightRecorderRegistry.Unregister(machine.FlightRecorder);
            machine.SetFlightRecorder(null);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void BindNodesToRecorder(BehaviorTreeGraph graph, BehaviorTreeFlightRecorder recorder)
        {
            if (graph == null) return;

            foreach (var node in graph.Nodes)
            {
                node.SetFlightRecorder(recorder);
            }
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void BeginTick(BehaviorTreeMachine machine)
        {
            machine?.FlightRecorder?.BeginTick();
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void BindRootScope(BehaviorTreeMachine machine, BehaviorTreeVariableScope scope)
        {
            machine?.FlightRecorder?.BindRootScope(scope);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void RegisterCallSite(
            RunBehaviorTreeGraphNode runNode,
            BehaviorTreeVariableScope scope,
            BehaviorTreeVariableScope parentScope)
        {
            var recorder = runNode?.FlightRecorder;
            if (recorder == null) return;

            recorder.RegisterCallSite(
                scope, parentScope, runNode.guid, BehaviorTreeRecordingNames.AssetNameOf(runNode));
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void NodeEnter(BehaviorTreeNode node)
        {
            node?.FlightRecorder?.NodeEnter(node);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void NodeExit(BehaviorTreeNode node, ExecutionStatus status)
        {
            node?.FlightRecorder?.NodeExit(node, status);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void GuardEval(BehaviorTreeNode guardedNode, ConditionalExecution guard, bool result)
        {
            guardedNode?.FlightRecorder?.GuardEval(guardedNode, guard, result);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void NodeSkipped(BehaviorTreeNode node, ConditionalExecution guard)
        {
            node?.FlightRecorder?.NodeSkipped(node, guard);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void NodeAborted(BehaviorTreeNode node, ConditionalExecution guard)
        {
            node?.FlightRecorder?.NodeAborted(node, guard);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void NodeTakenOver(
            BehaviorTreeNode victim, BehaviorTreeNode preemptor, ConditionalExecution guard)
        {
            victim?.FlightRecorder?.NodeTakenOver(victim, preemptor, guard);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void VariableWrite(
            BehaviorTreeNode writer,
            string key,
            BehaviorTreeVariableKind variableKind,
            object oldValue,
            object newValue)
        {
            writer?.FlightRecorder?.VariableWrite(writer, key, variableKind, oldValue, newValue);
        }

        /// <summary>Announces the node about to run a script graph. Always pair with <see cref="PopScriptGraphOwner"/>.</summary>
        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void PushScriptGraphOwner(BehaviorTreeNode node)
        {
            scriptGraphOwners.Push(node);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void PopScriptGraphOwner()
        {
            if (scriptGraphOwners.Count > 0) scriptGraphOwners.Pop();
        }

        /// <summary>
        /// A write made by a unit inside a Visual Scripting graph, attributed to the node that ran the graph
        /// when one announced itself and to a plain name when none did.
        /// </summary>
        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void ScriptGraphVariableWrite(
            BehaviorTreeMachine machine,
            string writerName,
            string key,
            BehaviorTreeVariableKind kind,
            object oldValue,
            object newValue)
        {
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
        /// A write made by something outside the tree, such as a perception sensor publishing a fact. A
        /// sensor should call this rather than reaching for the recorder, so that publishing costs it nothing
        /// outside the editor and dev builds.
        /// </summary>
        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
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
    }
}

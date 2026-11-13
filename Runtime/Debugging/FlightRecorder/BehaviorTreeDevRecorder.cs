using System.Collections.Generic;
using System.Diagnostics;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    public static class BehaviorTreeDevRecorder
    {
        public const string DevToolsDefine = "BH3_DEV_TOOLS";

        private const string EditorDefine = "UNITY_EDITOR";

        /// <summary>
        /// A Visual Scripting unit has no way back to the node that invoked its graph, so the node
        /// announces itself for the duration of the call. A stack because a graph can run another graph,
        /// and a static because graph evaluation is synchronous and single-threaded.
        /// </summary>
        private static readonly Stack<BehaviorTreeNode> scriptGraphOwners = new();

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void Attach(BehaviorTreeMachine machine, string treeName)
        {
            if (machine == null) return;

            var recorder = new BehaviorTreeFlightRecorder(
                BehaviorTreeRecordingNames.AgentNameOf(machine),
                treeName,
                BehaviorTreeFlightRecorderRegistry.DefaultEventCapacity,
                BehaviorTreeFlightRecorderRegistry.DefaultTraceCapacity);

            machine.SetFlightRecorder(recorder);
            BehaviorTreeFlightRecorderRegistry.Register(recorder);
        }

        /// <summary>A destroyed machine still answers FlightRecorder, and would read as recording.</summary>
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
            string variableName,
            BehaviorTreeVariableKind variableKind,
            object oldValue,
            object newValue)
        {
            writer?.FlightRecorder?.VariableWrite(writer, variableName, variableKind, oldValue, newValue);
        }

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

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void ScriptGraphVariableWrite(
            BehaviorTreeMachine machine,
            string writerName,
            string variableName,
            BehaviorTreeVariableKind variableKind,
            object oldValue,
            object newValue)
        {
            if (variableKind == BehaviorTreeVariableKind.None) return;

            var owner = scriptGraphOwners.Count > 0 ? scriptGraphOwners.Peek() : null;

            if (owner != null)
            {
                owner.FlightRecorder?.VariableWrite(owner, variableName, variableKind, oldValue, newValue);
                return;
            }

            machine?.FlightRecorder?.ExternalVariableWrite(
                writerName, variableName, oldValue, newValue, variableKind);
        }

        [Conditional(EditorDefine), Conditional(DevToolsDefine)]
        public static void ExternalVariableWrite(
            BehaviorTreeMachine machine,
            string writerName,
            string variableName,
            object oldValue,
            object newValue,
            BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.Object)
        {
            machine?.FlightRecorder?.ExternalVariableWrite(
                writerName, variableName, oldValue, newValue, variableKind);
        }
    }
}

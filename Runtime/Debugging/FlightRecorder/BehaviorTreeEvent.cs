using System;
using ArcaneOnyx.GraphCore;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>One flat struct for every kind, because the ring that holds them must not allocate.</summary>
    public readonly struct BehaviorTreeEvent
    {
        public const int MaxValueLength = 64;

        private readonly string name;

        public readonly BehaviorTreeEventKind Kind;
        public readonly int Tick;
        public readonly int Sequence;
        public readonly int Frame;
        public readonly float Time;
        public readonly int CallSiteId;
        public readonly Guid NodeGuid;
        public readonly Guid RelatedGuid;
        public readonly ExecutionStatus Status;
        public readonly bool GuardResult;
        public readonly Guid PreemptorGuid;
        public readonly string OldValue;
        public readonly string NewValue;
        public readonly BehaviorTreeVariableKind VariableKind;
        public readonly string WriterName;

        public string VariableName => name;

        public string SubTreeAssetName => name;

        public string PreemptorName => name;

        private BehaviorTreeEvent(
            BehaviorTreeEventKind kind,
            in BehaviorTreeEventStamp stamp,
            Guid nodeGuid,
            Guid relatedGuid = default,
            ExecutionStatus status = ExecutionStatus.None,
            bool guardResult = false,
            Guid preemptorGuid = default,
            string name = null,
            string oldValue = null,
            string newValue = null,
            BehaviorTreeVariableKind variableKind = BehaviorTreeVariableKind.None,
            string writerName = null)
        {
            Kind = kind;
            Tick = stamp.Tick;
            Sequence = stamp.Sequence;
            Frame = stamp.Frame;
            Time = stamp.Time;
            CallSiteId = stamp.CallSiteId;
            NodeGuid = nodeGuid;
            RelatedGuid = relatedGuid;
            Status = status;
            GuardResult = guardResult;
            PreemptorGuid = preemptorGuid;
            this.name = name;
            OldValue = oldValue;
            NewValue = newValue;
            VariableKind = variableKind;
            WriterName = writerName;
        }

        public static BehaviorTreeEvent NodeEntered(in BehaviorTreeEventStamp stamp, Guid nodeGuid)
        {
            return new BehaviorTreeEvent(BehaviorTreeEventKind.NodeEnter, stamp, nodeGuid);
        }

        public static BehaviorTreeEvent NodeExited(
            in BehaviorTreeEventStamp stamp, Guid nodeGuid, ExecutionStatus status)
        {
            return new BehaviorTreeEvent(BehaviorTreeEventKind.NodeExit, stamp, nodeGuid, status: status);
        }

        public static BehaviorTreeEvent NodeSkippedByGuard(
            in BehaviorTreeEventStamp stamp, Guid nodeGuid, Guid guardGuid)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.NodeSkipped, stamp, nodeGuid, relatedGuid: guardGuid);
        }

        public static BehaviorTreeEvent NodeAbortedByGuard(
            in BehaviorTreeEventStamp stamp, Guid nodeGuid, Guid guardGuid)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.NodeAborted, stamp, nodeGuid, relatedGuid: guardGuid);
        }

        public static BehaviorTreeEvent NodeTakenOver(
            in BehaviorTreeEventStamp stamp,
            Guid victimGuid,
            Guid guardGuid,
            string preemptorName,
            Guid preemptorGuid)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.NodeTakenOver,
                stamp,
                victimGuid,
                relatedGuid: guardGuid,
                preemptorGuid: preemptorGuid,
                name: preemptorName);
        }

        public static BehaviorTreeEvent GuardChanged(
            in BehaviorTreeEventStamp stamp, Guid guardGuid, Guid guardedNodeGuid, bool result)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.GuardEval,
                stamp,
                guardGuid,
                relatedGuid: guardedNodeGuid,
                guardResult: result);
        }

        public static BehaviorTreeEvent VariableWrittenByNode(
            in BehaviorTreeEventStamp stamp,
            Guid writerNodeGuid,
            string variableName,
            BehaviorTreeVariableKind variableKind,
            string oldValue,
            string newValue)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.VariableWrite,
                stamp,
                nodeGuid: Guid.Empty,
                relatedGuid: writerNodeGuid,
                name: variableName,
                oldValue: oldValue,
                newValue: newValue,
                variableKind: variableKind);
        }

        public static BehaviorTreeEvent VariableWrittenOutsideTree(
            in BehaviorTreeEventStamp stamp,
            string writerName,
            string variableName,
            BehaviorTreeVariableKind variableKind,
            string oldValue,
            string newValue)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.VariableWrite,
                stamp,
                nodeGuid: Guid.Empty,
                name: variableName,
                oldValue: oldValue,
                newValue: newValue,
                variableKind: variableKind,
                writerName: writerName);
        }

        public static BehaviorTreeEvent SubTreePushed(
            in BehaviorTreeEventStamp stamp, Guid runNodeGuid, string assetName)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.TreePushed, stamp, runNodeGuid, name: assetName);
        }

        public static BehaviorTreeEvent SubTreePopped(
            in BehaviorTreeEventStamp stamp, Guid runNodeGuid, string assetName)
        {
            return new BehaviorTreeEvent(
                BehaviorTreeEventKind.TreePopped, stamp, runNodeGuid, name: assetName);
        }

        /// <summary>
        /// Stringified here rather than at export because holding the object would box every struct on the
        /// one path that must not allocate, and would let a reference go on mutating after the event was
        /// recorded.
        /// </summary>
        public static string DescribeValue(object value)
        {
            if (value == null) return BehaviorTreeRecordingNames.NullValue;

            // ToString on a destroyed UnityEngine.Object reads "null", which is what the recording wants.
            var text = value.ToString();
            if (string.IsNullOrEmpty(text)) return BehaviorTreeRecordingNames.EmptyStringValue;

            return text.Length <= MaxValueLength ? text : text.Substring(0, MaxValueLength) + "…";
        }
    }
}

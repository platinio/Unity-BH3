using System;
using System.Collections.Generic;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Debugging
{
    /// <summary>
    /// Builds a <see cref="GuardTrace"/> at the moment a guard changes its result.
    ///
    /// <para>
    /// Two halves, because the two sides of a guard keep their history in completely different places.
    /// Behavior tree ports keep none at all, so the chain of behavior tree nodes is recovered by re-pulling
    /// each <see cref="ValueOutput"/> in the same instant — safe because those are pure fetch nodes, which is
    /// already a rule of the project and the reason fetching and acting are separate node kinds. Visual
    /// Scripting, by contrast, has already recorded every wire it pushed a value down, so its half is
    /// harvested rather than recomputed.
    /// </para>
    ///
    /// <para>
    /// Nothing here may throw into the tree. A guard evaluated during capture is the same code the tree just
    /// ran, so it should not fail — but a debugging aid that can break the thing it watches is worse than no
    /// debugging aid, so every entry point swallows.
    /// </para>
    /// </summary>
    public static class GuardTraceCapture
    {
        /// <summary>How far to follow a guard's inputs before giving up.</summary>
        private const int MaxDepth = 8;

        /// <summary>
        /// Walks a guard's inputs and captures what it was looking at.
        /// Returns null when there is nothing worth keeping.
        /// </summary>
        public static GuardTrace Capture(
            BehaviorTreeNode owner, ConditionalExecution guard, bool result, int tick, int sequence, int scopeId)
        {
            if (guard == null) return null;

            try
            {
                var chain = new List<GuardTraceNode>();
                var snapshots = new List<GuardGraphSnapshot>();

                chain.Add(new GuardTraceNode(
                    guard.guid, guard.NodeName, guard.GetType().Name,
                    result ? "true" : "false", 0, -1, -1));

                Walk(guard, chain, snapshots, new HashSet<Guid> { guard.guid }, parentIndex: 0, depth: 1);

                // A guard with nothing connected reads its own inline value. There is no chain to show and
                // the sentence around this already said what it returned, so keeping the trace would add a
                // row that says nothing.
                if (chain.Count <= 1) return null;

                return new GuardTrace(
                    tick, sequence, scopeId, guard.guid, owner != null ? owner.guid : Guid.Empty,
                    result, chain, snapshots);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Walk(
            BehaviorTreeNode node,
            List<GuardTraceNode> chain,
            List<GuardGraphSnapshot> snapshots,
            HashSet<Guid> seen,
            int parentIndex,
            int depth)
        {
            if (node?.valueInputs == null || depth > MaxDepth) return;

            foreach (var input in node.valueInputs)
            {
                var connection = input?.connection;
                if (connection?.source == null) continue;
                if (connection.source.behaviorTreeNode is not BehaviorTreeNode source) continue;

                // A DAG, not a tree: two inputs can share a source. Recording it twice would suggest it was
                // evaluated twice, which is a different claim about the graph than the one being made.
                if (!seen.Add(source.guid)) continue;

                var value = ReadValue(connection.source);

                // Re-pulled first, then harvested, so the chain value and the wires inside the graph come
                // from the same execution rather than from two moments a frame apart.
                var snapshotIndex = HarvestScriptGraphs(source, snapshots);

                var index = chain.Count;
                chain.Add(new GuardTraceNode(
                    source.guid, source.NodeName, source.GetType().Name, value, depth, parentIndex, snapshotIndex));

                Walk(source, chain, snapshots, seen, index, depth + 1);
            }
        }

        /// <summary>
        /// Re-pulls a port. A port with no fetch delegate has no value to give, which is not a failure — it is
        /// a node that produces its value some other way.
        /// </summary>
        private static string ReadValue(ValueOutput output)
        {
            if (output == null || !output.supportsFetch) return "(no value)";

            try
            {
                return BehaviorTreeEvent.Describe(output.GetPortValue());
            }
            catch (Exception e)
            {
                // A port that throws is worth showing rather than hiding: a guard reading an undeclared
                // variable throws here and that is very often the actual bug.
                return "(threw: " + e.GetType().Name + ")";
            }
        }

        /// <summary>
        /// Records the interior wires of every Visual Scripting graph a node owns.
        /// Returns the index of the first snapshot added, or -1.
        /// </summary>
        private static int HarvestScriptGraphs(BehaviorTreeNode node, List<GuardGraphSnapshot> snapshots)
        {
            var assets = node.scriptGraphAssets;
            if (assets == null) return -1;

            var first = -1;

            foreach (var asset in assets)
            {
                if (asset?.graph == null) continue;

                var snapshot = Harvest(node.guid, asset);
                if (snapshot == null) continue;

                if (first < 0) first = snapshots.Count;
                snapshots.Add(snapshot);
            }

            return first;
        }

        private static GuardGraphSnapshot Harvest(Guid ownerGuid, ScriptGraphAsset asset)
        {
            var wires = new List<GuardWireValue>();

            // No debug data means no wire values, and that is the normal state of a player build rather than
            // an error: the binding that provides it is installed by Unity's editor assembly. An empty
            // snapshot is still worth returning, so the viewer can say why it is empty.
            var pointer = asset.GetReference();
            var hasDebugData = pointer != null && pointer.hasDebugData;

            foreach (var connection in asset.graph.valueConnections)
            {
                if (connection == null) continue;

                // One unreadable connection is one wire missing from a snapshot, not a reason to lose the
                // rest of it. Resolving the ports can itself throw on a connection left dangling by an edit.
                try
                {
                    var value = "(not evaluated)";
                    var evaluated = false;

                    if (hasDebugData)
                    {
                        var data = pointer.GetElementDebugData<ValueConnection.DebugData>(connection);

                        if (data != null && data.assignedLastValue)
                        {
                            value = BehaviorTreeEvent.Describe(data.lastValue);
                            evaluated = true;
                        }
                    }

                    var source = connection.source;
                    var destination = connection.destination;

                    wires.Add(new GuardWireValue(
                        connection.guid,
                        source?.unit != null ? source.unit.guid : Guid.Empty,
                        source?.key,
                        destination?.unit != null ? destination.unit.guid : Guid.Empty,
                        destination?.key,
                        value,
                        evaluated));
                }
                catch (Exception)
                {
                }
            }

            return new GuardGraphSnapshot(ownerGuid, asset.name, wires);
        }
    }
}

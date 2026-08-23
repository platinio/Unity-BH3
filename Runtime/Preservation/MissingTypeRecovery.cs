using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Puts a node back where a <see cref="MissingType"/> placeholder is standing — onto the type it used to
    /// be, once that resolves again, or onto a replacement type someone picked.
    ///
    /// <para>
    /// Both are the same operation, deliberately. Restoring after a script comes back and retargeting onto a
    /// renamed type differ only in <em>which type is chosen and who chooses it</em>; everything after that —
    /// rebuilding from what was preserved, carrying inline values and connections by key, re-pointing the
    /// transitions and guards that named the old node — is identical, and a second implementation of it
    /// would be a second set of bugs.
    /// </para>
    ///
    /// <para>
    /// Runtime rather than Editor because the restore half runs wherever the asset loads. Nothing here
    /// touches the Editor; the surfaces that let a person choose a type live beside the inspector.
    /// </para>
    /// </summary>
    public static class MissingTypeRecovery
    {
        /// <summary>
        /// Restores every placeholder in <paramref name="graph"/> whose former type resolves again, and
        /// returns how many were put back. This is what makes re-adding a deleted script — or adding
        /// <c>[RenamedFrom]</c> to whatever replaced it — heal a tree with no further action.
        /// </summary>
        public static int RestoreResolvableNodes(BehaviorTreeGraph graph)
        {
            if (graph == null) return 0;

            // Scanned before anything is allocated. This runs on every load of every tree, including the
            // per-agent Instantiate that shared trees pay, and a tree with no placeholder -- which is nearly
            // all of them, nearly always -- must not pay a list for the privilege of being healthy.
            if (!HasPlaceholder(graph)) return 0;

            // Materialised because the collection is written while walking it.
            var placeholders = graph.Nodes.OfType<MissingType>().ToList();

            int restored = 0;

            foreach (var placeholder in placeholders)
            {
                if (!placeholder.TryResolveFormerType(out var resolved)) continue;
                if (!typeof(BehaviorTreeNode).IsAssignableFrom(resolved)) continue;

                // A placeholder that kept nothing would come back stripped of everything the node held,
                // which is worse than leaving it visibly broken for someone to retarget deliberately.
                if (!placeholder.HasPreservedState) continue;

                if (Retarget(graph, placeholder, resolved, out var failure) != null)
                {
                    restored++;
                    continue;
                }

                Debug.LogWarning(
                    $"[BH3] '{placeholder.formerType}' exists again, but the node preserved for it could not "
                    + $"be rebuilt, so its placeholder was left in place: {failure}");
            }

            return restored;
        }

        /// <summary>
        /// The node the graph actually holds under this guid, or null. By identity rather than by lookup
        /// alone, because the question callers need answered is "is this the object in the graph", and a
        /// guid match on a different instance is exactly the case worth catching.
        /// </summary>
        private static BehaviorTreeNode FindLiveNode(BehaviorTreeGraph graph, Guid guid)
        {
            foreach (var node in graph.Nodes)
            {
                if (node.guid == guid) return node;
            }

            return null;
        }

        /// <summary>Whether any node in the graph is a placeholder, without allocating to find out.</summary>
        public static bool HasPlaceholder(BehaviorTreeGraph graph)
        {
            if (graph == null) return false;

            foreach (var node in graph.Nodes)
            {
                if (node is MissingType) return true;
            }

            return false;
        }

        /// <summary>
        /// Replaces <paramref name="placeholder"/> with a node of <paramref name="target"/>, carrying over
        /// everything that still fits. Returns the new node, or null with a reason in
        /// <paramref name="failure"/>.
        /// </summary>
        public static BehaviorTreeNode Retarget(
            BehaviorTreeGraph graph, MissingType placeholder, Type target, out string failure)
        {
            failure = null;

            if (graph == null) { failure = "no graph."; return null; }
            if (placeholder == null) { failure = "no placeholder node."; return null; }
            if (target == null) { failure = "no target type."; return null; }

            // The picker is a non-modal window holding this reference across arbitrary editor time, and a
            // reimport swaps the whole graph out from under it without a domain reload to null it. The stale
            // placeholder then looks fine and shares its guid with the live one -- and Nodes is keyed by
            // guid, so proceeding either throws mid-swap on a duplicate key or rebuilds from state nothing
            // is showing. Refusing is the only outcome that is not silently wrong.
            if (!ReferenceEquals(FindLiveNode(graph, placeholder.guid), placeholder))
            {
                failure = "this placeholder is no longer in the tree -- the asset was reloaded. "
                          + "Reopen it and pick the replacement again.";
                return null;
            }

            var replacement = Rebuild(placeholder, target, out failure);
            if (replacement == null) return null;

            Replace(graph, placeholder, replacement);
            return replacement;
        }

        /// <summary>
        /// Builds a node of <paramref name="target"/> from what <paramref name="placeholder"/> preserved,
        /// without touching the graph.
        ///
        /// <para>
        /// The preserved document is handed back to the serializer with its type name swapped, which is why
        /// recovery needs no field mapping of its own: a member whose name and type still match carries its
        /// value over, and one that does not is dropped by the same rule that drops it from any other
        /// renamed type. <see cref="MissingType.formerObjects"/> goes along as the object table those
        /// values index into.
        /// </para>
        /// </summary>
        public static BehaviorTreeNode Rebuild(MissingType placeholder, Type target, out string failure)
        {
            failure = null;

            if (!typeof(BehaviorTreeNode).IsAssignableFrom(target))
            {
                failure = $"'{target.FullName}' is not a {nameof(BehaviorTreeNode)}.";
                return null;
            }

            if (target.IsAbstract)
            {
                failure = $"'{target.FullName}' is abstract, so there is nothing to create.";
                return null;
            }

            if (!placeholder.HasPreservedState) return RebuildFromDefaults(placeholder, target, out failure);

            if (!fsJsonParser.Parse(placeholder.formerValue, out var preserved).Succeeded || !preserved.IsDictionary)
            {
                failure = "the preserved state is not readable as JSON.";
                return null;
            }

            preserved.AsDictionary["$type"] = new fsData(RuntimeCodebase.SerializeType(target));

            try
            {
                var data = new SerializationData(
                    fsJsonPrinter.CompressedJson(preserved), placeholder.formerObjects);

                if (data.Deserialize() is not BehaviorTreeNode rebuilt)
                {
                    failure = $"the preserved state did not deserialize as a {nameof(BehaviorTreeNode)}.";
                    return null;
                }

                // The preserved document froze the node's position at the moment its type went missing, and
                // the placeholder has been draggable ever since -- moving the red node somewhere it can be
                // read is the first thing anyone does with one. Taking the placeholder's position rather
                // than the preserved one is what stops recovery silently undoing that.
                rebuilt.Position = placeholder.Position;
                rebuilt.position = placeholder.position;

                // The guid comes back from the preserved document and must be the one every transition and
                // breakpoint in the graph already names. It is the same guid by construction; this says so
                // out loud rather than trusting it, because a mismatch here presents as a node that loses
                // all its wiring for no visible reason.
                rebuilt.guid = placeholder.guid;

                return rebuilt;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                return null;
            }
        }

        /// <summary>
        /// The fallback for a placeholder written before placeholders kept anything. Everything the old node
        /// held is already gone; what can still be carried is its identity and its place on the canvas, so
        /// the transitions around it survive the swap.
        /// </summary>
        private static BehaviorTreeNode RebuildFromDefaults(MissingType placeholder, Type target, out string failure)
        {
            failure = null;

            try
            {
                var node = (BehaviorTreeNode)Activator.CreateInstance(target);
                node.guid = placeholder.guid;
                node.Position = placeholder.Position;
                node.position = placeholder.position;

                return node;
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                return null;
            }
        }

        /// <summary>
        /// Swaps <paramref name="replacement"/> in for <paramref name="placeholder"/>, keeping the wiring
        /// around it.
        ///
        /// <para>
        /// The order matters and is not free to change. Connections are recorded before the placeholder
        /// leaves the graph, because removing it disconnects them; the placeholder leaves before the
        /// replacement arrives, because both carry the same guid and the node collection is keyed by it;
        /// and the connections are restored after the replacement has defined its ports, because that is
        /// what decides which of them still have somewhere to land.
        /// </para>
        /// </summary>
        public static void Replace(BehaviorTreeGraph graph, MissingType placeholder, BehaviorTreeNode replacement)
        {
            var preservation = NodePreservation.Preserve(placeholder);

            var transitions = graph.Transitions.Where(t => t.source == placeholder || t.destination == placeholder).ToList();
            foreach (var transition in transitions) graph.Transitions.Remove(transition);

            var guardedBy = graph.Nodes.OfType<ConditionalExecution>().Where(g => g.Owner == placeholder).ToList();

            graph.Nodes.Remove(placeholder);

            // Add fires AfterAdd -> Define(), so the replacement's ports exist before anything is restored
            // onto them.
            graph.Nodes.Add(replacement);

            preservation.RestoreTo(replacement);

            foreach (var transition in transitions)
            {
                // The same transition objects, re-pointed and put back, so a transition keeps its guid and
                // with it anything that recorded one -- breakpoints and the flight recorder both do.
                transition.SetupTransition(
                    transition.source == placeholder ? replacement : transition.source,
                    transition.destination == placeholder ? replacement : transition.destination,
                    transition.TransitionIndex);

                graph.Transitions.Add(transition);
            }

            foreach (var guard in guardedBy) guard.UpdateOwner(replacement);
        }
    }
}

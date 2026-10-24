using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Authoring
{
    /// <summary>
    /// Arranges a tree so nothing is drawn on top of anything else: parents centred over their children,
    /// siblings left to right in priority order, the nodes that feed a node's ports in columns beside it.
    ///
    /// <para>
    /// A tree is rearranged when something overlaps, or when a sub-tree node is no longer the size the
    /// arrangement was built around. An author who has placed nodes by hand has said what they want, and a
    /// tidy canvas is already what this would produce; a crossed-out one is the only thing worth overruling.
    /// </para>
    ///
    /// <para>
    /// The nested trees go first, because arranging one changes how big it is and a sub-tree node is drawn
    /// as the whole tree it runs (<see cref="SubTreeFootprint"/>). Laying out a parent before its branches
    /// would place boxes whose size is about to change.
    /// </para>
    /// </summary>
    public static class BehaviorTreeLayout
    {
        public const float GapBetweenSiblings = 8 * CanvasGrid.Spacing;
        public const float GapBetweenGenerations = 27 * CanvasGrid.Spacing;
        public const float GapBeforeFeeders = 6 * CanvasGrid.Spacing;
        public const float GapBetweenFeeders = 4 * CanvasGrid.Spacing;
        public const float GapBetweenUnrelatedBlocks = 20 * CanvasGrid.Spacing;

        /// <summary>
        /// How far a wire may run before the node at its end is given a read of its own: about the width
        /// of one node, so a read serves the consumer it sits beside and at most its neighbour. A branch
        /// is drawn as the whole tree it runs, metres tall, and a wire to the far side of one is a wire
        /// nobody follows even when the gap between the boxes reads as small.
        /// </summary>
        public const float ReachOfAWire = 200.0f;

        /// <summary>
        /// Arranges <paramref name="asset"/> and every tree it runs, deepest first, and saves the ones that
        /// changed. A tree whose nodes do not overlap and whose wires all reach keeps its author's
        /// arrangement, with any node off the canvas grid moved onto it. Returns a line per tree.
        /// </summary>
        public static IReadOnlyList<ArrangedTree> Arrange(BehaviorTreeGraphAsset asset) =>
            Walk(asset, leavingTidyTreesAlone: true);

        /// <summary>
        /// The same, on every tree whether it needs it or not. For when these rules have changed and the
        /// trees arranged under the old ones are tidy by the old measure.
        /// </summary>
        public static IReadOnlyList<ArrangedTree> RearrangeEverything(BehaviorTreeGraphAsset asset) =>
            Walk(asset, leavingTidyTreesAlone: false);

        private static IReadOnlyList<ArrangedTree> Walk(BehaviorTreeGraphAsset asset, bool leavingTidyTreesAlone)
        {
            var arranged = new List<ArrangedTree>();

            ArrangeBranchesFirst(asset, new HashSet<BehaviorTreeGraphAsset>(), arranged, leavingTidyTreesAlone);

            return arranged;
        }

        /// <summary>
        /// How many pairs of nodes are drawn over each other, counting the band a node's guards occupy
        /// above it. Zero is a canvas a person can read.
        /// </summary>
        public static int CountOverlappingPairs(BehaviorTreeGraph graph)
        {
            if (graph == null) return 0;

            var drawn = PlaceableNodesOf(graph).Select(DrawnRectOf).ToList();

            var pairs = 0;

            for (var first = 0; first < drawn.Count; first++)
            {
                for (var second = first + 1; second < drawn.Count; second++)
                {
                    if (drawn[first].Overlaps(drawn[second])) pairs++;
                }
            }

            return pairs;
        }

        public static int CountNodesOffTheGrid(BehaviorTreeGraph graph) =>
            graph == null ? 0 : PlaceableNodesOf(graph).Count(node => !CanvasGrid.IsOnTheGrid(node.Position.position));

        private static void ArrangeBranchesFirst(
            BehaviorTreeGraphAsset asset, HashSet<BehaviorTreeGraphAsset> alreadySeen, List<ArrangedTree> arranged,
            bool leavingTidyTreesAlone)
        {
            if (asset == null || asset.graph == null || !alreadySeen.Add(asset)) return;

            foreach (var branch in BranchTreesOf(asset.graph)) ArrangeBranchesFirst(branch, alreadySeen, arranged, leavingTidyTreesAlone);

            var resized = ResizeBranchNodes(asset.graph, out var arrangedAroundAStaleWidth);
            var overlapsBefore = CountOverlappingPairs(asset.graph);
            var offTheGridBefore = CountNodesOffTheGrid(asset.graph);

            // Asked twice on purpose: once about the tree as its author left it, where a wire running the
            // length of a tidy canvas is the whole complaint, and once about where this pass has just put
            // things, because a tree that arrived as a pile had no distances worth judging.
            var readsAdded = GiveFarConsumersTheirOwnRead(asset.graph);
            var wasArranged = !leavingTidyTreesAlone
                              || overlapsBefore > 0
                              || readsAdded > 0
                              || arrangedAroundAStaleWidth;

            if (!wasArranged && offTheGridBefore > 0)
            {
                SnapToTheGrid(asset.graph);
                wasArranged = CountOverlappingPairs(asset.graph) > 0;
            }

            if (wasArranged)
            {
                Place(asset.graph);

                var readsAddedOnceItWasLaidOut = GiveFarConsumersTheirOwnRead(asset.graph);
                if (readsAddedOnceItWasLaidOut > 0) Place(asset.graph);

                readsAdded += readsAddedOnceItWasLaidOut;
            }

            if (wasArranged || resized || offTheGridBefore > 0) SaveOnlyThisTree(asset);

            arranged.Add(new ArrangedTree(
                AssetDatabase.GetAssetPath(asset), overlapsBefore, wasArranged, CountOverlappingPairs(asset.graph), readsAdded,
                offTheGridBefore));
        }

        /// <summary>
        /// Moves every node to where a drag would leave it, keeping the author's arrangement. A node off the
        /// grid is not untidy enough to overrule how somebody laid the tree out.
        /// </summary>
        private static void SnapToTheGrid(BehaviorTreeGraph graph)
        {
            foreach (var node in PlaceableNodesOf(graph))
            {
                node.Position = new Rect(CanvasGrid.Nearest(node.Position.position), node.Position.size);
            }
        }

        /// <summary>
        /// Saved one asset at a time rather than through AssetDatabase.SaveAssets, which writes every dirty
        /// asset in the project — including canvas state a designer has not decided to keep.
        /// </summary>
        private static void SaveOnlyThisTree(BehaviorTreeGraphAsset asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static IEnumerable<BehaviorTreeGraphAsset> BranchTreesOf(BehaviorTreeGraph graph) =>
            graph.Nodes
                .OfType<RunBehaviorTreeGraphNode>()
                .Select(node => node.BehaviorTreeGraphAsset)
                .Where(branch => branch != null)
                .Distinct();

        /// <summary>
        /// Writes each sub-tree node the size it is actually drawn at. Until this runs, a branch added from
        /// code is still the 150x100 it was created at and the canvas is measured against a fiction.
        /// </summary>
        private static bool ResizeBranchNodes(BehaviorTreeGraph graph, out bool arrangedAroundAStaleWidth)
        {
            var resized = false;
            arrangedAroundAStaleWidth = false;

            foreach (var node in graph.Nodes.OfType<RunBehaviorTreeGraphNode>())
            {
                if (node.BehaviorTreeGraphAsset == null) continue;

                var drawnAs = SubTreeFootprint.DrawnAs(node);
                if (drawnAs.size == node.Position.size) continue;

                arrangedAroundAStaleWidth |= IsArrangedAroundAStaleWidth(node, drawnAs);
                node.Position = drawnAs;
                resized = true;
            }

            return resized;
        }

        /// <summary>
        /// Whether the width this node is spaced around is no longer the width it will be drawn at.
        /// <para>
        /// Deliberately not "has the branch been collapsed". Collapsing is one way to get here, but the
        /// question is whether the <em>arrangement</em> is stale, and the two part company in both
        /// directions: a tree saved collapsed is already spaced correctly for it and must be left alone
        /// when it is opened, and a branch somebody added nodes to needs arranging again without anything
        /// having been collapsed at all.
        /// </para>
        /// <para>
        /// A grid step or less is not stale. Snapping a branch's nodes to the grid moves each edge of its
        /// frame by up to half a step, and re-spacing every caller over that would throw away the
        /// arrangement the snapping just kept. Growth that does reach a neighbour is an overlap, which is
        /// caught on its own.
        /// </para>
        /// </summary>
        private static bool IsArrangedAroundAStaleWidth(RunBehaviorTreeGraphNode node, Rect drawnAs) =>
            Mathf.Abs(drawnAs.width - node.Position.width) > CanvasGrid.Spacing;

        private static void Place(BehaviorTreeGraph graph)
        {
            var feeders = FeedersByTheNodeTheyFeed(graph);
            var placed = new HashSet<BehaviorTreeNode>();

            var left = 0.0f;

            foreach (var root in RootsOf(graph))
            {
                var branch = BuildBranch(graph, root, feeders, placed);
                PlaceBranch(branch, left, 0.0f);
                left += branch.Width + GapBetweenUnrelatedBlocks;
            }

            PlaceLooseNodes(graph, feeders, placed, left);
        }

        /// <summary>
        /// The nodes a walk starts from: the entry first, then anything else nothing transitions into. A
        /// tree being authored has half-connected branches, and they are laid out rather than left where
        /// they fell.
        /// </summary>
        private static IEnumerable<BehaviorTreeNode> RootsOf(BehaviorTreeGraph graph)
        {
            var withParents = new HashSet<BehaviorTreeNode>(
                graph.Transitions.Select(transition => transition.destination as BehaviorTreeNode).Where(node => node != null));

            return ControlNodesOf(graph)
                .Where(node => !withParents.Contains(node))
                .OrderByDescending(node => node == graph.EntryNode)
                .ThenBy(node => node.Position.xMin);
        }

        /// <summary>Nodes that take part in the tree itself, as opposed to feeding a port on one.</summary>
        private static HashSet<BehaviorTreeNode> ControlNodesOf(BehaviorTreeGraph graph)
        {
            var control = new HashSet<BehaviorTreeNode>();

            if (graph.EntryNode != null) control.Add(graph.EntryNode);

            foreach (var transition in graph.Transitions)
            {
                if (transition.source is BehaviorTreeNode source) control.Add(source);
                if (transition.destination is BehaviorTreeNode destination) control.Add(destination);
            }

            return control;
        }

        private static IEnumerable<BehaviorTreeNode> PlaceableNodesOf(BehaviorTreeGraph graph) =>
            graph.Nodes.Where(node => node is not PlaceHolderNode && node is not ConditionalExecution);

        private static Rect DrawnRectOf(BehaviorTreeNode node)
        {
            var drawn = node.Position;
            drawn.yMin -= SubTreeFootprint.RoomAbove(node);

            return drawn;
        }

        private static Branch BuildBranch(
            BehaviorTreeGraph graph, BehaviorTreeNode node,
            IReadOnlyDictionary<BehaviorTreeNode, List<Feeder>> feeders, HashSet<BehaviorTreeNode> placed)
        {
            placed.Add(node);

            var branch = new Branch(
                node,
                SubTreeFootprint.RoomAbove(node),
                feeders.TryGetValue(node, out var itsFeeders) ? itsFeeders : new List<Feeder>());

            foreach (var transition in graph.ChildTransitionsInPriorityOrder(node))
            {
                if (transition.destination is not BehaviorTreeNode child) continue;
                if (placed.Contains(child)) continue;

                branch.Add(BuildBranch(graph, child, feeders, placed));
            }

            return branch;
        }

        private static void PlaceBranch(Branch branch, float left, float top)
        {
            branch.PlaceAt(left, top);

            if (branch.Children.Count == 0) return;

            var childrenTop = branch.Node.Position.yMax + GapBetweenGenerations
                            + branch.Children.Max(child => child.RoomAbove);
            var childLeft = left + (branch.Width - branch.ChildrenWidth) * 0.5f;

            foreach (var child in branch.Children)
            {
                PlaceBranch(child, childLeft, childrenTop);
                childLeft += child.Width + GapBetweenSiblings;
            }
        }

        /// <summary>
        /// Nodes connected to nothing at all, parked in a row to the right of the tree so they are visible
        /// and in nobody's way.
        /// </summary>
        private static void PlaceLooseNodes(
            BehaviorTreeGraph graph,
            IReadOnlyDictionary<BehaviorTreeNode, List<Feeder>> feeders, HashSet<BehaviorTreeNode> placed, float left)
        {
            var fed = new HashSet<BehaviorTreeNode>(feeders.Values.SelectMany(list => list).Select(feeder => feeder.Node));

            foreach (var node in PlaceableNodesOf(graph))
            {
                if (placed.Contains(node) || fed.Contains(node)) continue;

                var loose = new Branch(node, SubTreeFootprint.RoomAbove(node), new List<Feeder>());
                loose.PlaceAt(left, 0.0f);
                left += loose.Width + GapBetweenSiblings;
            }
        }

        /// <summary>
        /// Gives a node its own copy of the value it reads when the one it shares is too far away to
        /// follow. A reader can only sit beside one of its consumers; every other one is left with a wire
        /// across the canvas, which in a tree of branches runs for thousands of pixels. Copying the read
        /// costs nothing — two Get Variable nodes on one key are one fact, read twice.
        /// <para>
        /// Only a node that reads nothing itself is copied. One with inputs of its own is the head of a
        /// chain, and copying a chain copies work rather than a lookup.
        /// </para>
        /// </summary>
        private static int GiveFarConsumersTheirOwnRead(BehaviorTreeGraph graph)
        {
            var control = ControlNodesOf(graph);
            var added = 0;

            foreach (var reader in PlaceableNodesOf(graph).ToList())
            {
                if (control.Contains(reader) || !ReadsNothingItself(graph, reader)) continue;

                var wires = WiresFrom(graph, reader);
                if (wires.Count < 2) continue;

                foreach (var wire in wires.OrderBy(wire => DistanceBetween(reader, ConsumerOf(wire))).Skip(1).ToList())
                {
                    if (DistanceBetween(reader, ConsumerOf(wire)) <= ReachOfAWire) continue;

                    Rewire(graph, wire, CopyOf(graph, reader));
                    added++;
                }
            }

            return added;
        }

        private static List<PortValueConnection> WiresFrom(BehaviorTreeGraph graph, BehaviorTreeNode reader) =>
            graph.valueConnections.Where(wire => (wire.source?.behaviorTreeNode as BehaviorTreeNode) == reader).ToList();

        private static bool ReadsNothingItself(BehaviorTreeGraph graph, BehaviorTreeNode reader) =>
            graph.valueConnections.All(wire => (wire.destination?.behaviorTreeNode as BehaviorTreeNode) != reader);

        /// <summary>Where a wire ends on the canvas: a guard is drawn on its owner, so that is what it reaches.</summary>
        private static BehaviorTreeNode ConsumerOf(PortValueConnection wire)
        {
            var reader = wire.destination?.behaviorTreeNode as BehaviorTreeNode;

            return reader is ConditionalExecution guard ? guard.Owner : reader;
        }

        private static float DistanceBetween(BehaviorTreeNode from, BehaviorTreeNode to)
        {
            if (to == null) return 0.0f;

            var here = from.Position;
            var there = to.Position;
            var across = Mathf.Max(0.0f, Mathf.Max(here.xMin - there.xMax, there.xMin - here.xMax));
            var down = Mathf.Max(0.0f, Mathf.Max(here.yMin - there.yMax, there.yMin - here.yMax));

            return Mathf.Sqrt(across * across + down * down);
        }

        private static void Rewire(BehaviorTreeGraph graph, PortValueConnection wire, BehaviorTreeNode reader)
        {
            var destination = wire.destination;
            var key = wire.source.key;

            graph.valueConnections.Remove(wire);
            reader.valueOutputs.First(port => port.key == key).ValidlyConnectTo(destination);
        }

        /// <summary>
        /// A second node reading the same thing: the same type, the same serialized state and the same
        /// values typed into its ports, with a guid and a place on the canvas of its own.
        /// </summary>
        private static BehaviorTreeNode CopyOf(BehaviorTreeGraph graph, BehaviorTreeNode reader)
        {
            var copy = (BehaviorTreeNode)Activator.CreateInstance(reader.GetType());
            copy.Position = reader.Position;
            graph.Nodes.Add(copy);

            foreach (var field in StateOf(reader.GetType())) field.SetValue(copy, field.GetValue(reader));
            foreach (var typed in reader.defaultValues) copy.defaultValues[typed.Key] = typed.Value;

            return copy;
        }

        /// <summary>
        /// What a node keeps, minus what makes it that node: a copy needs a guid of its own, and the
        /// layout is about to give it a place of its own.
        /// </summary>
        private static IEnumerable<FieldInfo> StateOf(Type type)
        {
            for (var declaring = type; declaring != null; declaring = declaring.BaseType)
            {
                const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

                foreach (var field in declaring.GetFields(Declared))
                {
                    if (field.Name == "guid" || field.Name.Contains("osition")) continue;
                    if (!field.IsDefined(typeof(Unity.VisualScripting.SerializeAttribute), true)
                        && !field.IsDefined(typeof(SerializeField), true)) continue;

                    yield return field;
                }
            }
        }

        /// <summary>
        /// Every node that only exists to feed a port — a literal, a variable read, a Function — filed
        /// under the tree node it ends up feeding, with how many hops away it is so a chain can be drawn as
        /// one column per hop. A feeder of a guard is filed under the guard's owner, which is where the
        /// guard is drawn.
        /// </summary>
        private static Dictionary<BehaviorTreeNode, List<Feeder>> FeedersByTheNodeTheyFeed(BehaviorTreeGraph graph)
        {
            var control = ControlNodesOf(graph);
            var readers = ReadersBySource(graph);
            var byConsumer = new Dictionary<BehaviorTreeNode, List<Feeder>>();

            foreach (var node in PlaceableNodesOf(graph))
            {
                if (control.Contains(node)) continue;

                var consumer = ConsumerOf(node, readers, control, out var hops);
                if (consumer == null) continue;

                if (!byConsumer.TryGetValue(consumer, out var feeders))
                {
                    feeders = new List<Feeder>();
                    byConsumer[consumer] = feeders;
                }

                feeders.Add(new Feeder(node, hops));
            }

            return byConsumer;
        }

        private static Dictionary<BehaviorTreeNode, List<BehaviorTreeNode>> ReadersBySource(BehaviorTreeGraph graph)
        {
            var readers = new Dictionary<BehaviorTreeNode, List<BehaviorTreeNode>>();

            foreach (var connection in graph.valueConnections)
            {
                var source = connection.source?.behaviorTreeNode as BehaviorTreeNode;
                var reader = connection.destination?.behaviorTreeNode as BehaviorTreeNode;
                if (source == null || reader == null) continue;

                if (!readers.TryGetValue(source, out var list))
                {
                    list = new List<BehaviorTreeNode>();
                    readers[source] = list;
                }

                list.Add(reader);
            }

            return readers;
        }

        private static BehaviorTreeNode ConsumerOf(
            BehaviorTreeNode feeder, IReadOnlyDictionary<BehaviorTreeNode, List<BehaviorTreeNode>> readers,
            ICollection<BehaviorTreeNode> control, out int hops)
        {
            var reached = new HashSet<BehaviorTreeNode> { feeder };
            var wave = new List<BehaviorTreeNode> { feeder };

            for (hops = 1; wave.Count > 0 && hops < 32; hops++)
            {
                var next = new List<BehaviorTreeNode>();

                foreach (var node in wave)
                {
                    if (!readers.TryGetValue(node, out var itsReaders)) continue;

                    foreach (var reader in itsReaders)
                    {
                        var owner = reader is ConditionalExecution guard ? guard.Owner : reader;
                        if (owner == null) continue;
                        if (control.Contains(owner)) return owner;
                        if (reached.Add(reader)) next.Add(reader);
                    }
                }

                wave = next;
            }

            return null;
        }

        private readonly struct Feeder
        {
            public Feeder(BehaviorTreeNode node, int hops)
            {
                Node = node;
                Hops = hops;
            }

            public BehaviorTreeNode Node { get; }

            public int Hops { get; }
        }

        /// <summary>
        /// One node of the tree together with everything drawn around it — the guards above, the feeders
        /// beside, the branches below — and the width all of that needs.
        /// </summary>
        private sealed class Branch
        {
            private readonly List<List<Feeder>> feederColumns;
            private readonly List<Branch> children = new List<Branch>();

            public Branch(BehaviorTreeNode node, float roomAbove, IEnumerable<Feeder> feeders)
            {
                Node = node;
                RoomAbove = roomAbove;
                feederColumns = feeders
                    .GroupBy(feeder => feeder.Hops)
                    .OrderBy(column => column.Key)
                    .Select(column => column.ToList())
                    .ToList();
            }

            public BehaviorTreeNode Node { get; }

            public float RoomAbove { get; }

            public IReadOnlyList<Branch> Children => children;

            public float Width => Mathf.Max(BlockWidth, ChildrenWidth);

            public float ChildrenWidth =>
                children.Count == 0
                    ? 0.0f
                    : children.Sum(child => child.Width) + (children.Count - 1) * GapBetweenSiblings;

            public void Add(Branch child) => children.Add(child);

            public void PlaceAt(float left, float top)
            {
                var blockLeft = left + (Width - BlockWidth) * 0.5f;
                var nodeLeft = CanvasGrid.RoundUp(blockLeft + FeedersWidth);
                var nodeTop = CanvasGrid.RoundUp(top);

                Node.Position = new Rect(nodeLeft, nodeTop, Node.Position.width, Node.Position.height);

                PlaceFeeders(nodeLeft, nodeTop);
            }

            private float BlockWidth => FeedersWidth + Node.Position.width;

            private float FeedersWidth =>
                feederColumns.Count == 0
                    ? 0.0f
                    : feederColumns.Sum(WidthOf) + feederColumns.Count * GapBeforeFeeders;

            /// <summary>
            /// Stacked downward from the top of what they feed, because that is where its ports are: a
            /// node lays its ports out under its header, and a sub-tree frame carries its parameters just
            /// below its name. Centred on the node instead, a column beside a branch two thousand pixels
            /// tall ends up a screen below the port it is wired to.
            /// </summary>
            private void PlaceFeeders(float nodeLeft, float portsBeginAt)
            {
                var columnRight = nodeLeft - GapBeforeFeeders;

                foreach (var column in feederColumns)
                {
                    var columnWidth = WidthOf(column);
                    var top = portsBeginAt;

                    foreach (var feeder in column)
                    {
                        var size = feeder.Node.Position.size;
                        feeder.Node.Position = new Rect(CanvasGrid.RoundUp(columnRight - size.x), CanvasGrid.RoundUp(top), size.x, size.y);
                        top = feeder.Node.Position.yMax + GapBetweenFeeders;
                    }

                    columnRight -= columnWidth + GapBeforeFeeders;
                }
            }

            private static float WidthOf(List<Feeder> column) => column.Max(feeder => feeder.Node.Position.width);
        }
    }

    /// <summary>What <see cref="BehaviorTreeLayout.Arrange"/> did to one tree.</summary>
    public sealed class ArrangedTree
    {
        public ArrangedTree(
            string tree, int overlapsBefore, bool arranged, int overlapsAfter, int readsAdded, int offTheGridBefore)
        {
            this.tree = tree;
            this.overlapsBefore = overlapsBefore;
            this.arranged = arranged;
            this.overlapsAfter = overlapsAfter;
            this.readsAdded = readsAdded;
            this.offTheGridBefore = offTheGridBefore;
        }

        public string tree { get; }

        public int overlapsBefore { get; }

        public bool arranged { get; }

        public int overlapsAfter { get; }

        public int readsAdded { get; }

        public int offTheGridBefore { get; }
    }
}

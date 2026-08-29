using System.IO;
using System.Linq;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Where a container decides that a child may start, and what happens when that decision is refused.
    ///
    /// <para>
    /// The rule these pin is one sentence: <b>a node that did not enter must not be ticked</b>. The
    /// same-frame half of it is settled inside the node itself, by reusing the entry verdict. This fixture
    /// is about the <em>across-frame</em> half, which no node can settle for itself: a container that
    /// entered its child once and then ticked it every frame afterwards had no second opportunity to notice
    /// the entry had been turned away. The guard flips true a frame later, the tick sails through, and
    /// <c>OnUpdate</c> runs against state <c>OnEnter</c> never set.
    /// </para>
    ///
    /// <para>
    /// So entry moved to where the tick is — <see cref="ContainerNode.TickChild"/> — and the tests below
    /// come in two kinds. The behavioural ones drive a <see cref="Repeater"/>, which is the shape the bug
    /// actually shipped in: a decorator that restarts its child forever is the standard root of a tree that
    /// must not end. The last one is a convention gate, because this defect appeared independently in six
    /// of the seven decorators — a rule that has already been broken six times is worth enforcing rather
    /// than documenting.
    /// </para>
    /// </summary>
    [TestFixture]
    public class ChildEntryOnTickTests
    {
        private static T AddNode<T>(BehaviorTreeGraph graph, float x = 0.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(x, 100.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, 0);
            graph.Transitions.Add(transition);
        }

        /// <summary>
        /// <c>Entry -&gt; Repeater -&gt; leaf</c>, with a plain doorman guard on the leaf that starts true.
        /// The guard has to be wired through a graph rather than attached by hand: arming a guard onto its
        /// owner is something <see cref="BehaviorTreeGraph.OnAwake"/> does while walking the transitions.
        /// </summary>
        private static Repeater GuardedLeaf(
            out ScriptedNode leaf, out CountingGuard guard, out BehaviorTreeGraph graph)
        {
            graph = new BehaviorTreeGraph();
            var repeater = AddNode<Repeater>(graph);

            leaf = new ScriptedNode(ExecutionStatus.Success) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(leaf);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, leaf);

            guard = AddNode<CountingGuard>(graph, -300.0f);
            guard.UpdateOwner(leaf);

            graph.OnAwake();

            return repeater;
        }

        #region A refused entry is retried on a later tick, not ticked past

        [Test]
        public void AChildRefusedAtEntryIsEnteredByTheTickThatFinallyAcceptsIt()
        {
            var repeater = GuardedLeaf(out var leaf, out var guard, out _);
            guard.Result = false;

            repeater.OnNodeEnter();
            Assert.AreEqual(ExecutionStatus.Running, repeater.OnUpdateInternal());

            Assert.AreEqual(0, leaf.EnterCalls, "Precondition: the guard turned the child away at the door.");
            Assert.AreEqual(0, leaf.UpdateCalls, "And a child that never entered must not have run.");

            guard.Result = true;
            repeater.OnUpdateInternal();

            Assert.AreEqual(1, leaf.EnterCalls,
                "The tick that finds the guard passing must retry the entry, not tick past it. Ticking a "
                + "node whose OnEnter never ran is the whole hazard: a WaitTime counts down a timer it "
                + "never set and reports Success as though the wait had elapsed.");
            Assert.AreEqual(1, leaf.UpdateCalls,
                "And the one tick that ran is the one that followed the one entry.");
        }

        [Test]
        public void AChildIsNotRunOnAFrameItsGuardWouldHaveRefused()
        {
            var repeater = GuardedLeaf(out var leaf, out var guard, out _);

            repeater.OnNodeEnter();
            repeater.OnUpdateInternal();

            Assert.AreEqual(1, leaf.UpdateCalls, "Precondition: the guard was true, so the child ran once.");

            // The world moves between two ticks, which is the ordinary case and not an exotic one.
            guard.Result = false;
            Assert.AreEqual(ExecutionStatus.Running, repeater.OnUpdateInternal());

            Assert.AreEqual(1, leaf.UpdateCalls,
                "A restart decided on the previous frame is a decision acted on a frame after it was made. "
                + "By the time the child is ticked the guard has already said no, and a plain "
                + "ConditionalExecution is a doorman -- it is not asked again once a node is running, so "
                + "nothing downstream would catch it.");
            Assert.AreEqual(1, leaf.EnterCalls, "A refused restart is not an entry either.");
        }

        [Test]
        public void ARestartedChildIsEnteredTickedAndExitedExactlyOncePerTick()
        {
            var repeater = GuardedLeaf(out var leaf, out _, out _);
            repeater.OnNodeEnter();

            const int ticks = 5;
            for (int tick = 0; tick < ticks; tick++)
            {
                Assert.AreEqual(ExecutionStatus.Running, repeater.OnUpdateInternal(),
                    "A Repeater never completes, whatever the child returns.");
            }

            Assert.AreEqual(ticks, leaf.UpdateCalls, "One run per tick.");
            Assert.AreEqual(ticks, leaf.EnterCalls, "Each preceded by exactly one entry,");
            Assert.AreEqual(ticks, leaf.ExitCalls, "and followed by exactly one exit.");
            Assert.IsFalse(leaf.IsRunning,
                "Nothing is left entered between ticks. The child used to be restarted at the end of a "
                + "tick and so sat entered across the gap, which is what let a guard change in that gap go "
                + "unnoticed.");
        }

        [Test]
        public void TheRootsChildEntersOnTheFirstTickRatherThanAtEntry()
        {
            var leaf = new ScriptedNode(ExecutionStatus.Running);
            var entry = new Entry().WithChildren(leaf);

            entry.OnNodeEnter();

            Assert.AreEqual(0, leaf.EnterCalls,
                "The machine enters the root once and then ticks it for the life of the agent, so an entry "
                + "refused here would never be retried. Entry belongs with the tick.");

            entry.OnUpdateInternal();

            Assert.AreEqual(1, leaf.EnterCalls, "The first tick is what starts the tree.");
            Assert.AreEqual(1, leaf.UpdateCalls, "And it runs on the same tick, so nothing is delayed by this.");
        }

        #endregion

        #region The rule, enforced rather than documented

        /// <summary>
        /// The four containers allowed to call <c>OnUpdateInternal</c> directly, and why each one is not
        /// simply an exception waiting to become the seventh bug.
        /// </summary>
        private static readonly string[] PairsEntryWithItsTickItself =
        {
            // Declares TickChild, and calls OnUpdateInternal inside it.
            "ContainerNode.cs",

            // Declares the context's own TickChild -- the universal form of ContainerNode's, and the one a
            // migrated composite uses. It pairs entry with the tick in the same call, and answers "is this
            // child running" from the agent's instance on a shared tree or from the node on a clone, which
            // is precisely the pairing this rule exists to require.
            "BTContext.cs",

            // Its own base call -- BehaviorTreeNode.OnUpdateInternal chaining to BaseGraphNode's.
            "BehaviorTreeNode.cs",

            // Keep a callOnEnter flag, which says something IsRunning cannot: *this child is next*, as
            // distinct from *this child is live*. They walk several children within one tick.
            "Selector.cs",
            "Sequence.cs",

            // Parallel enters every child before ticking any of them, and retires a failed one through its
            // status array rather than offering it the door again.
            "ParallelSelector.cs",
            "ParallelSequence.cs",
        };

        [Test]
        public void EveryOtherContainerTicksItsChildrenThroughTickChild()
        {
            var offenders = Directory
                .EnumerateFiles(RuntimeNodesFolder(), "*.cs", SearchOption.AllDirectories)
                .Where(path => !PairsEntryWithItsTickItself.Contains(Path.GetFileName(path)))
                .Where(path => File.ReadAllText(path).Contains(".OnUpdateInternal()"))
                .Select(Path.GetFileName)
                .ToArray();

            Assert.IsEmpty(offenders,
                $"Ticks a child directly rather than through ContainerNode.TickChild: "
                + $"{string.Join(", ", offenders)}. A container that ticks a child it did not just enter has "
                + "no way to know the entry was refused, and this exact defect appeared independently in six "
                + "of the seven decorators. Use TickChild, or list the file above with the reason it pairs "
                + "entry with the tick itself.");
        }

        /// <summary>
        /// <c>Runtime/Nodes</c>, found by locating <c>ContainerNode.cs</c> rather than by a hardcoded path,
        /// so relocating the package does not turn this gate into a test that scans nothing and passes.
        /// </summary>
        private static string RuntimeNodesFolder()
        {
            var found = Directory
                .GetFiles(Application.dataPath, "ContainerNode.cs", SearchOption.AllDirectories)
                .ToArray();

            Assert.AreEqual(1, found.Length,
                $"Expected exactly one ContainerNode.cs under {Application.dataPath}, found {found.Length}.");

            // .../Runtime/Nodes/ContainerNodes/ContainerNode.cs -> .../Runtime/Nodes
            var folder = Path.GetDirectoryName(Path.GetDirectoryName(found[0]));

            Assert.IsTrue(Directory.Exists(folder), $"Runtime node folder {folder} does not exist.");

            return folder;
        }

        #endregion
    }
}

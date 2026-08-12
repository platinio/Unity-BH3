using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// When a reactive guard is allowed to recompute.
    ///
    /// <para>
    /// The load-bearing decision is that a trigger does not cause evaluation — it marks the guard dirty, and
    /// the tick decides. That keeps the model deterministic (nothing evaluates outside the tick, so
    /// component execution order cannot change a frame's decision) while still paying only for guards the
    /// tree actually asks about.
    /// </para>
    /// </summary>
    [TestFixture]
    public class GuardTriggerTests
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

        /// <summary>Entry -> Sequence -> a child that never finishes, with a counting reactive guard on the Sequence.</summary>
        private static Sequence Guarded(out CountingReactiveGuard guard, out BehaviorTreeGraph graph)
        {
            graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph);

            var child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            guard = AddNode<CountingReactiveGuard>(graph, 200.0f);
            guard.UpdateOwner(sequence);

            return sequence;
        }

        [Test]
        public void AGuardWithNoTriggersEvaluatesEveryTick()
        {
            var sequence = Guarded(out var guard, out var graph);
            graph.OnAwake();

            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();
            sequence.OnUpdateInternal();

            Assert.AreEqual(3, guard.Evaluations,
                "no triggers means always due, which is the behaviour a guard had before triggers existed -- "
                + "so an asset authored without them is unchanged");
        }

        /// <summary>
        /// Spec test 5. An interval that has not elapsed makes the guard clean, and a clean guard hands back
        /// its cached answer without running its condition at all.
        /// </summary>
        [Test]
        public void AGuardWhoseIntervalHasNotElapsedIsNotEvaluated()
        {
            var sequence = Guarded(out var guard, out var graph);
            guard.AddTrigger(GuardTrigger.Interval(5.0f));
            graph.OnAwake();

            sequence.OnNodeEnter();

            int afterEntry = guard.Evaluations;

            sequence.OnUpdateInternal();
            sequence.OnUpdateInternal();
            sequence.OnUpdateInternal();

            Assert.AreEqual(afterEntry, guard.Evaluations,
                "three ticks inside one interval cost one bool check each, not three graph runs");
        }

        /// <summary>
        /// Spec test 4. Entry ignores the dirty flag entirely. A stale false costs latency; a stale true
        /// enters a branch whose precondition no longer holds -- the animation starts, the token is claimed,
        /// and the abort has to unwind it.
        /// </summary>
        [Test]
        public void EntryAlwaysEvaluatesEvenWhenTheGuardIsClean()
        {
            var sequence = Guarded(out var guard, out var graph);
            guard.AddTrigger(GuardTrigger.Interval(5.0f));
            graph.OnAwake();

            sequence.OnNodeEnter();
            sequence.OnUpdateInternal();

            int beforeSecondEntry = guard.Evaluations;
            var child = (ScriptedNode)sequence.GetChildren()[0];
            int childEntriesSoFar = child.EnterCalls;

            // The world moved, but no trigger fired. A cached answer here would admit a branch on a
            // precondition that is no longer true.
            guard.Result = false;
            sequence.OnNodeExit();
            sequence.OnNodeEnter();

            Assert.AreEqual(beforeSecondEntry + 1, guard.Evaluations, "entry recomputed rather than trusting the cache");
            Assert.AreEqual(childEntriesSoFar, child.EnterCalls,
                "and so the branch was refused this time, which is the whole point of paying for it");
        }

        [Test]
        public void ACleanGuardKeepsAnsweringWhatItLastDecided()
        {
            var sequence = Guarded(out var guard, out var graph);
            guard.AddTrigger(GuardTrigger.Interval(5.0f));
            graph.OnAwake();

            sequence.OnNodeEnter();

            // Flip the underlying answer without letting any trigger fire.
            guard.Result = false;

            Assert.AreEqual(ExecutionStatus.Running, sequence.OnUpdateInternal(),
                "a guard nothing marked dirty keeps its previous answer, rather than silently re-reading the world");
        }

        [Test]
        public void TriggersCombineAsAnOr()
        {
            var sequence = Guarded(out var guard, out var graph);
            guard.AddTrigger(GuardTrigger.Interval(600.0f));
            guard.AddTrigger(GuardTrigger.EveryFrame());
            graph.OnAwake();

            sequence.OnNodeEnter();
            int afterEntry = guard.Evaluations;

            sequence.OnUpdateInternal();

            Assert.AreEqual(afterEntry + 1, guard.Evaluations,
                "the interval says no and every-frame says yes; either one is enough");
        }

        /// <summary>
        /// A guard watching nothing can never become dirty on its own. Deliberately not treated as
        /// every-frame: a guard that never re-checks is a bug its author should see, which is why bt_verify
        /// reports an empty key list rather than the runtime guessing.
        /// </summary>
        [Test]
        public void AKeyTriggerWatchingNothingNeverWakesTheGuard()
        {
            var sequence = Guarded(out var guard, out var graph);
            guard.AddTrigger(GuardTrigger.KeyChanged());
            graph.OnAwake();

            sequence.OnNodeEnter();
            int afterEntry = guard.Evaluations;

            guard.Result = false;
            sequence.OnUpdateInternal();
            sequence.OnUpdateInternal();

            Assert.AreEqual(afterEntry, guard.Evaluations);
        }

        /// <summary>
        /// The counters are the seam OnKeyChanged is built on, so they have to count changes rather than
        /// writes. A fact recomputed every frame to the same value must not make a guard look dirty every
        /// frame — that would turn the cheapest trigger into the most expensive one.
        /// </summary>
        [Test]
        public void TheWriterCountsChangesRatherThanWrites()
        {
            var agent = new GameObject("Agent", typeof(Unity.VisualScripting.Variables));

            try
            {
                var writer = AgentVariableWriter.On(agent);

                Assert.AreEqual(0, writer.VersionOf("hasTarget"), "a key nothing has written has never moved");

                Assert.IsTrue(writer.Write("Sensor", "hasTarget", true));
                Assert.AreEqual(1, writer.VersionOf("hasTarget"));

                Assert.IsFalse(writer.Write("Sensor", "hasTarget", true), "same value, so the write is dropped");
                Assert.AreEqual(1, writer.VersionOf("hasTarget"), "and the version must not move with it");

                Assert.IsTrue(writer.Write("Sensor", "hasTarget", false));
                Assert.AreEqual(2, writer.VersionOf("hasTarget"));
            }
            finally
            {
                Object.DestroyImmediate(agent);
            }
        }
    }
}

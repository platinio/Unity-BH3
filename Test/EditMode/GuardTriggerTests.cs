using ArcaneOnyx.BehaviorTree.Authoring;
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
        /// <summary>Entry -> Sequence -> a child that never finishes, with a counting reactive guard on the Sequence.</summary>
        private static Sequence Guarded(out CountingReactiveGuard guard, out BehaviorTreeGraph graph)
        {
            graph = new BehaviorTreeGraph();
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(graph, 0.0f, 100.0f);

            var child = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            BehaviorTreeAuthoring.Connect(graph, graph.EntryNode, sequence);
            BehaviorTreeAuthoring.Connect(graph, sequence, child);

            guard = BehaviorTreeAuthoring.AddNode<CountingReactiveGuard>(graph, 200.0f, 100.0f);
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

        /// <summary>
        /// The counter means "changed", not "was written". A tree node that writes the same value every frame
        /// would otherwise make every guard watching that key recompute every frame, which turns the cheapest
        /// trigger into the most expensive one — the exact cost the counter exists to avoid.
        /// </summary>
        [Test]
        public void RewritingTheSameValueDoesNotMoveTheVersion()
        {
            var agent = new GameObject("Agent", typeof(Unity.VisualScripting.Variables));

            try
            {
                var writer = AgentVariableWriter.On(agent);

                Assert.IsTrue(writer.SetAgentVariable("hasTarget", true), "first write is a change");
                Assert.AreEqual(1, writer.VersionOf("hasTarget"));

                Assert.IsFalse(writer.SetAgentVariable("hasTarget", true), "same value again is not");
                Assert.IsFalse(writer.SetAgentVariable("hasTarget", true));
                Assert.AreEqual(1, writer.VersionOf("hasTarget"),
                    "so a fact republished every frame costs its watchers nothing");

                Assert.IsTrue(writer.SetAgentVariable("hasTarget", false));
                Assert.AreEqual(2, writer.VersionOf("hasTarget"));
            }
            finally
            {
                Object.DestroyImmediate(agent);
            }
        }

        /// <summary>
        /// A version is only ever read on a GameObject that runs a tree, because a guard is a node inside
        /// one. Writing one anywhere else is storage nobody queries — and since the lookup is get-or-add, it
        /// would attach BH3 components to whatever the write happened to target. A tree writing a flag on a
        /// door must not change the door.
        /// </summary>
        [Test]
        public void WritingToSomethingThatIsNotAnAgentVersionsNothingAndAddsNothing()
        {
            var door = new GameObject("Door", typeof(Unity.VisualScripting.Variables));

            try
            {
                Assert.IsFalse(AgentVariableWriter.SetOn(door, "isOpen", true),
                    "nothing here can watch the key, so the write is not versioned");

                Assert.IsFalse(door.TryGetComponent<AgentVariableWriter>(out _),
                    "and the door is left exactly as it was");

                Assert.AreEqual(true, Unity.VisualScripting.Variables.Object(door).Get("isOpen"),
                    "the write itself still happens -- only the bookkeeping is skipped");
            }
            finally
            {
                Object.DestroyImmediate(door);
            }
        }

        [Test]
        public void WritingToAnAgentIsVersioned()
        {
            var agent = new GameObject("Agent", typeof(Unity.VisualScripting.Variables), typeof(BehaviorTreeMachine));

            try
            {
                Assert.IsTrue(AgentVariableWriter.SetOn(agent, "hasTarget", true), "an agent can be watched");
                Assert.AreEqual(1, AgentVariableWriter.On(agent).VersionOf("hasTarget"));

                Assert.IsFalse(AgentVariableWriter.SetOn(agent, "hasTarget", true), "same value, no change");
                Assert.AreEqual(1, AgentVariableWriter.On(agent).VersionOf("hasTarget"));
            }
            finally
            {
                Object.DestroyImmediate(agent);
            }
        }

        /// <summary>
        /// A blank key is skipped at evaluation, so storing one would leave a list that looks populated and
        /// can never mark the guard dirty — and would hide the guard from the check for watching nothing.
        /// </summary>
        [Test]
        public void BlankKeysAreNotStored()
        {
            var trigger = GuardTrigger.KeyChanged("hasTarget", "", null, "   ", "inRange");

            CollectionAssert.AreEqual(new[] { "hasTarget", "inRange" }, trigger.Keys);
            Assert.AreEqual(2, trigger.UsableKeyCount());

            Assert.IsEmpty(GuardTrigger.KeyChanged("", null).Keys,
                "a trigger built entirely from blanks watches nothing, and says so");
            Assert.AreEqual(0, GuardTrigger.KeyChanged((string[])null).UsableKeyCount());
        }

        /// <summary>
        /// Hand-authored blanks reach the list without going through the factory, so the count has to be of
        /// keys that could actually wake the guard rather than of entries present.
        /// </summary>
        [Test]
        public void AListOfBlanksCountsAsWatchingNothing()
        {
            var trigger = GuardTrigger.KeyChanged();
            trigger.Keys.Add("");
            trigger.Keys.Add("  ");

            Assert.AreEqual(2, trigger.Keys.Count);
            Assert.AreEqual(0, trigger.UsableKeyCount(), "which is what bt_verify reports on");
        }
    }
}

using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// The two capabilities of a <see cref="ReactiveGuard"/>, one test per capability per setting.
    ///
    /// <para>
    /// <b>Stops Its Own Branch</b> (<c>abortsOwner</c>) — the guard turns false while its owner is running:
    /// does the branch end?
    /// <br/>
    /// <b>Takes Over Lower Priority</b> (<c>preempts</c>) — the guard turns true while a <em>lower-priority</em>
    /// sibling is running: does its branch claim the slot?
    /// </para>
    ///
    /// <para>
    /// They are separate flags rather than one mode because the useful combinations are not a spectrum. The
    /// committed swing — takes over, does not self-abort — is unreachable with a single switch, and it is the
    /// setting a melee attack wants: bid for control the moment the target is in range, then finish the
    /// animation even if the target steps back out. Testing them as a matrix is what shows they are genuinely
    /// independent rather than two names for the same thing, which is exactly what a test of only the default
    /// pairing cannot show.
    /// </para>
    ///
    /// <para>
    /// Each fixture is the same two-branch selector, differing only in the guard's capabilities and in which
    /// direction the fact is driven. Held apart from <c>ReactiveGuardIntegrationTests</c>, which is about a
    /// different question: whether a guard can be woken at all.
    /// </para>
    /// </summary>
    public class ReactiveGuardCapabilityTests : PlayModeAgentFixture
    {
        private System.Guid guarded;
        private System.Guid fallback;

        // ---------------------------------------------------------------- Takes Over Lower Priority

        /// <summary>
        /// <c>preempts: true</c> — the guard turns true while the lower-priority branch is running, and takes
        /// the slot on that same frame.
        /// </summary>
        [UnityTest]
        public IEnumerator TakesOverLowerPriorityOnClaimsTheSlotFromARunningBranch()
        {
            var machine = SpawnTwoBranches(startsEligible: false, stopsItsOwnBranch: true, takesOverLowerPriority: true);

            yield return Frames(3);

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, fallback), "The lower-priority branch holds while the guard is false.");
            Assert.IsFalse(Entered(recorder, guarded), "and the guarded branch has not started.");

            int mark = Mark(recorder);
            PublishFact(machine, "eligible", true);

            yield return Frames(3);

            Assert.IsTrue(EnteredSince(recorder, mark, guarded),
                "With Takes Over Lower Priority on, becoming eligible is a bid for the slot,");
            Assert.IsTrue(TakenOverSince(recorder, mark, fallback),
                "and the running branch below loses it -- recorded as a takeover, which names a cause outside "
                + "that branch rather than a precondition of its own that stopped holding.");
        }

        /// <summary>
        /// <c>preempts: false</c> — the guard turns true, and nothing happens. The branch is eligible but
        /// cannot interrupt; it waits to be reached by ordinary selection, which a running sibling prevents.
        /// </summary>
        [UnityTest]
        public IEnumerator TakesOverLowerPriorityOffWaitsRatherThanInterrupting()
        {
            var machine = SpawnTwoBranches(startsEligible: false, stopsItsOwnBranch: true, takesOverLowerPriority: false);

            yield return Frames(3);

            var recorder = machine.FlightRecorder;
            int mark = Mark(recorder);

            PublishFact(machine, "eligible", true);

            yield return Frames(5);

            Assert.IsFalse(EnteredSince(recorder, mark, guarded),
                "A guard that cannot take over may not start its branch while a lower-priority one is "
                + "running, however true its condition became. A Selector resumes at its running child and "
                + "does not re-check the siblings above it.");
            Assert.IsFalse(TakenOverSince(recorder, mark, fallback),
                "and nothing may be recorded as losing a slot, because no slot changed hands.");
        }

        // ---------------------------------------------------------------- Stops Its Own Branch

        /// <summary>
        /// <c>abortsOwner: true</c> — the guard turns false mid-run and its own branch ends immediately.
        /// </summary>
        [UnityTest]
        public IEnumerator StopsItsOwnBranchOnEndsTheRunWhenTheConditionFails()
        {
            var machine = SpawnTwoBranches(startsEligible: true, stopsItsOwnBranch: true, takesOverLowerPriority: true);

            yield return Frames(3);

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, guarded), "The guarded branch runs while its condition holds.");

            int mark = Mark(recorder);
            PublishFact(machine, "eligible", false);

            yield return Frames(3);

            Assert.IsTrue(AbortedSince(recorder, mark, guarded),
                "With Stops Its Own Branch on, the precondition failing kills the branch mid-run -- recorded "
                + "as an abort, which names the branch's own guard as the cause.");
            Assert.IsTrue(EnteredSince(recorder, mark, fallback),
                "and the slot falls through to the branch below.");
        }

        /// <summary>
        /// <c>abortsOwner: false</c> — the guard turns false mid-run and the branch carries on regardless.
        /// This is the committed swing.
        /// </summary>
        [UnityTest]
        public IEnumerator StopsItsOwnBranchOffKeepsRunningWhenTheConditionFails()
        {
            var machine = SpawnTwoBranches(startsEligible: true, stopsItsOwnBranch: false, takesOverLowerPriority: true);

            yield return Frames(3);

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, guarded), "The guarded branch runs while its condition holds.");

            int mark = Mark(recorder);
            PublishFact(machine, "eligible", false);

            yield return Frames(5);

            Assert.IsFalse(AbortedSince(recorder, mark, guarded),
                "With Stops Its Own Branch off, a precondition that stops holding must not end the branch. "
                + "This is the committed swing: the guard bids for control when the target comes into range, "
                + "and once the animation is under way it finishes even if the target steps back out.");
            Assert.IsFalse(EnteredSince(recorder, mark, fallback),
                "and the branch below must not be given the slot, because the branch above never gave it up.");
        }

        #region Fixture

        /// <summary>
        /// Entry -&gt; Repeater -&gt; Selector -&gt; [ guarded, fallback ], both long-running so that the only
        /// thing which can ever change which one holds the slot is the guard.
        /// </summary>
        private BehaviorTreeMachine SpawnTwoBranches(
            bool startsEligible, bool stopsItsOwnBranch, bool takesOverLowerPriority)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var guardedNode = Add<WaitTime>(graph, -400.0f, 500.0f);
            var fallbackNode = Add<WaitTime>(graph, 400.0f, 500.0f);

            FeedFloat(graph, guardedNode, guardedNode.Time, 999.0f);
            FeedFloat(graph, fallbackNode, fallbackNode.Time, 999.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, guardedNode);
            Connect(graph, selector, fallbackNode);

            var read = ReadAgentVariable(graph, "eligible", -750.0f, 350.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 350.0f);
            guard.UpdateOwner(guardedNode);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.SetCapabilities(abortsOwner: stopsItsOwnBranch, preempts: takesOverLowerPriority);
            guard.AddTrigger(GuardTrigger.KeyChanged("eligible"));

            guarded = guardedNode.guid;
            fallback = fallbackNode.guid;

            return Spawn(tree, (_, variables) => variables.declarations.Set("eligible", startsEligible));
        }

        private static IEnumerator Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                yield return null;
            }
        }

        #endregion
    }
}

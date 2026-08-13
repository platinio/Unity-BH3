using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Nodes whose whole behaviour is a clock: <see cref="WaitTime"/> counting down
    /// <c>Time.deltaTime</c>, and <see cref="Cooldown"/> comparing against <c>Time.time</c>.
    ///
    /// <para>
    /// <b>Neither had its timing asserted anywhere.</b> <c>WaitTime</c> appears throughout the suite purely
    /// as scaffolding — the flight-recorder fixture feeds it <c>999f</c> specifically so it never finishes —
    /// so nothing in the project checks that a Wait ever completes. Break the subtraction in
    /// <c>OnUpdate</c> and the entire edit-mode suite stays green.
    /// </para>
    ///
    /// <para>
    /// <c>Cooldown</c> is sharper still. <c>DecoratorNodeTests</c> covers the blocking half with the comment
    /// <i>"Edit mode advances no game time between these two runs, so the cooldown is still active"</i> — the
    /// test depends on the clock being frozen. That makes the recharge observable but leaves the
    /// <em>expiry</em> — the half a designer is actually relying on — untested by construction.
    /// </para>
    ///
    /// <para>
    /// <see cref="Time.captureDeltaTime"/> pins a frame to an exact slice of game time, so these assert on
    /// elapsed time rather than on how busy the machine was.
    /// </para>
    /// </summary>
    public class NodeTimingIntegrationTests : PlayModeAgentFixture
    {
        private const float FrameSeconds = 0.05f;

        [TearDown]
        public void ReleaseTheClock()
        {
            Time.captureDeltaTime = 0.0f;
        }

        /// <summary>
        /// A Wait runs for its duration and then succeeds — and the machine stops ticking once it has, which
        /// is the <c>lastExecutionStatus</c> latch doing its job.
        /// </summary>
        [UnityTest]
        public IEnumerator AWaitRunsForItsDurationAndThenSucceeds()
        {
            var tree = NewTree();
            var graph = tree.graph;

            // Deliberately no Repeater: the root completing is the observable here.
            var wait = Add<WaitTime>(graph, 0.0f, 100.0f);
            FeedFloat(graph, wait, wait.Time, 0.25f);

            Connect(graph, graph.EntryNode, wait);

            var machine = Spawn(tree);

            // 0.10s elapsed against a 0.25s wait.
            for (int frame = 0; frame < 2; frame++)
            {
                Time.captureDeltaTime = FrameSeconds;
                yield return null;
            }

            Assert.AreEqual(ExecutionStatus.Running, machine.LastExecutionStatus,
                "Two frames is 0.10s of game time, so a 0.25s wait must still be running -- a Wait that "
                + "succeeds immediately would satisfy every other test in the project.");

            // Past 0.25s.
            for (int frame = 0; frame < 6; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(ExecutionStatus.Success, machine.LastExecutionStatus,
                "and once the duration has elapsed it must finish. Nothing else in the suite asserts that a "
                + "Wait ever completes; the flight-recorder fixture feeds it 999f precisely so it does not.");
        }

        /// <summary>
        /// The machine stops ticking a tree that finished. <c>Update</c> returns early once
        /// <c>lastExecutionStatus</c> is Success or Failure, which is why a tree without a Repeater under
        /// Entry runs exactly once — the single most common way a hand-authored tree appears dead.
        /// </summary>
        [UnityTest]
        public IEnumerator AFinishedTreeIsNotTickedAgain()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 100.0f);
            FeedFloat(graph, wait, wait.Time, 0.05f);

            Connect(graph, graph.EntryNode, wait);

            var machine = Spawn(tree);

            Time.captureDeltaTime = FrameSeconds;

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(ExecutionStatus.Success, machine.LastExecutionStatus);

            int entriesWhenFinished = EnterCount(machine.FlightRecorder, wait.guid);

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(entriesWhenFinished, EnterCount(machine.FlightRecorder, wait.guid),
                "A completed tree is latched off. If the node is re-entered, the machine is restarting a "
                + "finished tree and every 'runs once then goes quiet' diagnosis in the docs is wrong.");
        }

        /// <summary>
        /// A Cooldown blocks its child while recharging and <em>lets it through again once the duration
        /// elapses</em>. The second half is what edit mode cannot see.
        ///
        /// <para>
        /// The observable is a variable the child writes, not the recorder's entry count. That is deliberate:
        /// <c>Cooldown.OnUpdate</c> ticks its child by calling <c>OnUpdateInternal</c> directly, and
        /// <c>OnUpdateInternal</c> does not enter a node that is not running. So "the child ran" and "the
        /// child was entered" are separate facts here, and only the first one is what a cooldown expiring
        /// means. Counting entries would measure the bookkeeping instead of the behaviour.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ACooldownBlocksItsChildThenLetsItThroughAgain()
        {
            var machine = SpawnCooldownTree(0.3f);

            Time.captureDeltaTime = FrameSeconds;

            yield return null;

            var writer = AgentVariableWriter.On(machine.gameObject);

            Assert.AreEqual(true, Variables.Object(machine.gameObject).Get("marker"),
                "A cooldown that has never fired lets the child through immediately.");

            // Put the marker back so any later run is visible as a fresh change.
            AgentVariableWriter.SetOn(machine.gameObject, "marker", false);
            int afterReset = writer.VersionOf("marker");

            // 0.10s elapsed, well inside the 0.3s recharge.
            for (int frame = 0; frame < 2; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(afterReset, writer.VersionOf("marker"),
                "While recharging the child must not run at all -- the decorator reports Failure without "
                + "ticking it.");

            // Past the recharge.
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.Greater(writer.VersionOf("marker"), afterReset,
                "Once the duration has elapsed the child has to run again. If this never moves, the cooldown "
                + "latched permanently -- which no edit-mode test can distinguish from correct behaviour, "
                + "because DecoratorNodeTests relies on the clock being frozen to observe the blocking half.");
        }

        /// <summary>
        /// A Cooldown enters its child on every pass, not only the first.
        ///
        /// <para>
        /// <c>Cooldown.OnUpdate</c> reaches its child through <c>OnUpdateInternal</c>, which runs
        /// <c>OnUpdate</c> and nothing else — it does not enter a node that is not running. The only place the
        /// child is ever entered is <c>Cooldown.OnEnter</c>, and that returns early while recharging. Since
        /// the Repeater above exits and re-enters the decorator every frame, every entry after the first
        /// lands during the recharge and is refused, so by the time the cooldown expires there is no path
        /// left that enters the child at all.
        /// </para>
        ///
        /// <para>
        /// The consequence is not bookkeeping. <c>OnEnter</c> is where a node establishes its per-run state:
        /// a <see cref="WaitTime"/> child sets its timer there, so it would wait the full duration on the
        /// first pass and then complete instantly on every pass afterwards; an animation node re-triggers its
        /// animator there; a navigation node re-issues its destination. A child that is updated but never
        /// entered runs with whatever state it finished the previous pass holding.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ACooldownEntersItsChildOnEveryPass()
        {
            var machine = SpawnCooldownTree(0.2f);

            Time.captureDeltaTime = FrameSeconds;

            // 1.0s of game time against a 0.2s recharge: several passes.
            for (int frame = 0; frame < 20; frame++)
            {
                yield return null;
            }

            var child = RunningNode<SetVariable>(machine);

            Assert.Greater(EnterCount(machine.FlightRecorder, child.guid), 1,
                "The child must be entered on each pass. Entered exactly once across a run spanning several "
                + "expiries means it is being updated without OnEnter ever running again -- so a Wait child "
                + "never resets its timer and an animation child never re-triggers.");
        }

        /// <summary>
        /// Entry -> Repeater -> Cooldown -> Set Variable, with the marker starting false.
        /// </summary>
        private BehaviorTreeMachine SpawnCooldownTree(float duration)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var cooldown = Add<Cooldown>(graph, 0.0f, 250.0f);

            var child = Add<SetVariable>(graph, 0.0f, 400.0f);
            SetPrivateField(child, "VariableKind", VariableKind.Object);
            FeedString(graph, child, child.Key, "marker");
            FeedBool(graph, child, child.Value, true);

            FeedFloat(graph, cooldown, cooldown.Duration, duration);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, cooldown);
            Connect(graph, cooldown, child);

            return Spawn(tree, (_, variables) => variables.declarations.Set("marker", false));
        }
    }
}

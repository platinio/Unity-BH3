using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// <see cref="GuardTriggerKind.EveryInterval"/> against real game time.
    ///
    /// <para>
    /// <b>This kind cannot be tested in edit mode at all</b>, and not by oversight:
    /// <c>ReactiveGuard.Now</c> is <c>Application.isPlaying ? Time.time : 0f</c>, so once a guard has
    /// evaluated, <c>secondsSinceEvaluated</c> is permanently <c>0 - 0</c> and no positive interval is ever
    /// due. Every interval in the edit-mode suite is therefore a stand-in for "a trigger that will never
    /// fire" — <c>Interval(5f)</c> and <c>Interval(600f)</c> are interchangeable there, and those tests would
    /// still pass if <c>IsDue</c> for this kind simply returned false. They pin the caching economy, which is
    /// worth pinning, but the interval elapsing is unasserted anywhere.
    /// </para>
    ///
    /// <para>
    /// <c>Deviation</c> and the <c>Reroll</c> that applies it have no coverage in any mode. It exists so that
    /// two hundred agents sharing a 0.2s timer do not land on one frame, and it multiplies its interval by a
    /// random factor — a arithmetic slip there degrades silently into either "never fires" or "every frame",
    /// both of which look like working software until a scene is full.
    /// </para>
    ///
    /// <para>
    /// Time is pinned with <see cref="Time.captureDeltaTime"/> so a frame advances the clock by an exact
    /// amount. Without it these would be frame-rate roulette on a loaded machine, which is how a suite earns
    /// a reputation for flaking and stops being trusted.
    /// </para>
    /// </summary>
    public class GuardScheduleIntegrationTests : PlayModeAgentFixture
    {
        private const float FrameSeconds = 0.05f;

        /// <summary>
        /// Deliberately not a multiple of <see cref="FrameSeconds"/>. An interval that lands exactly on a
        /// frame boundary is decided by whether accumulated float time reads as slightly over or slightly
        /// under, which is the one genuinely fragile thing about timing a guard. At 0.17s the trigger comes
        /// due on the fourth frame (0.20s elapsed) with most of a frame to spare either side.
        /// </summary>
        private const float Interval = 0.17f;

        /// <summary>Frames per fire at the constants above: ceil(0.17 / 0.05).</summary>
        private const int FramesPerFire = 4;

        private Random.State randomState;

        [SetUp]
        public void PinTheRandomSequence()
        {
            // GuardTrigger.Reroll draws from UnityEngine.Random for the deviation, so a deviated interval is
            // otherwise a different test every run. Seeding makes the draws reproducible; the state is put
            // back afterwards so this does not shift the sequence any later test sees.
            randomState = Random.state;
            Random.InitState(20260813);
        }

        [TearDown]
        public void ReleaseTheClockAndRandom()
        {
            // Both leak into every later test in the run if they are not put back.
            Time.captureDeltaTime = 0.0f;
            Random.state = randomState;
        }

        /// <summary>
        /// An interval trigger fires when the interval elapses — repeatedly, and far less often than every
        /// frame. The first assertion in this project that an interval is ever <em>due</em>.
        /// </summary>
        [UnityTest]
        public IEnumerator AnIntervalTriggerRecomputesWhenTheIntervalElapses()
        {
            var machine = SpawnPolledGuard(GuardTrigger.Interval(Interval));

            Time.captureDeltaTime = FrameSeconds;

            var guard = RunningNode<ReactiveGuard>(machine);

            // Settle, then measure from a known point so entry evaluations are not counted as scheduled ones.
            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            int start = guard.Evaluations;

            const int frames = 20;

            for (int frame = 0; frame < frames; frame++)
            {
                yield return null;
            }

            int scheduled = guard.Evaluations - start;
            const int expected = frames / FramesPerFire;

            // Exact but for one: the clock is pinned, so this does not depend on how fast the machine is.
            // The single frame of slack is phase, not noise -- the guard last evaluated somewhere inside the
            // settle window rather than on the frame counting started, so its cycle can be offset by up to
            // one frame relative to this loop. Nothing here draws a random number.
            Assert.That(scheduled, Is.InRange(expected - 1, expected + 1),
                $"{frames} frames of {FramesPerFire} should fire {expected} times; saw {scheduled}. Zero means "
                + "the trigger never comes due at all -- which is precisely what an edit-mode test cannot "
                + $"distinguish from correct behaviour, since there the clock never moves. {frames} means the "
                + "interval is gating nothing and the guard is paying the every-frame cost silently.");
        }

        /// <summary>
        /// A deviated interval still fires at a sane rate. <c>Reroll</c> has no coverage anywhere, and its
        /// failure modes are both silent: a factor that collapses toward zero turns the cheapest trigger into
        /// an every-frame one, and one that inflates turns a reactive guard into a guard that never reacts.
        /// </summary>
        [UnityTest]
        public IEnumerator ADeviatedIntervalStillFiresWithinItsBand()
        {
            const float deviation = 0.08f;

            var machine = SpawnPolledGuard(GuardTrigger.Interval(Interval, deviation));

            Time.captureDeltaTime = FrameSeconds;

            var guard = RunningNode<ReactiveGuard>(machine);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            int start = guard.Evaluations;

            const int frames = 20;

            for (int frame = 0; frame < frames; frame++)
            {
                yield return null;
            }

            int scheduled = guard.Evaluations - start;

            // Reroll draws per fire, so the count is a property of the whole draw sequence rather than of one
            // number. The seed pinned in SetUp makes that sequence identical every run; the band is derived
            // from the extremes the deviation permits rather than from any particular draw, so a harmless
            // change to how Reroll consumes randomness does not turn this red.
            int fastest = Mathf.CeilToInt(frames / Mathf.Ceil((Interval - deviation) / FrameSeconds));
            int slowest = frames / Mathf.CeilToInt((Interval + deviation) / FrameSeconds);

            Assert.That(scheduled, Is.InRange(slowest, fastest),
                $"A {Interval}s +/- {deviation}s interval over {frames} frames admits {slowest} to {fastest} "
                + $"recomputes; saw {scheduled}. Below that band Reroll is inflating the interval and the "
                + "guard has stopped reacting; above it, Reroll is collapsing toward zero and the cheapest "
                + "trigger has quietly become the most expensive one.");
        }

        /// <summary>
        /// A guard with no triggers evaluates every tick. Documented as the behaviour a guard had before
        /// triggers existed, and covered in edit mode; repeated here because it is the baseline the two tests
        /// above are measured against. If this one did not report roughly one evaluation per frame, a low
        /// count elsewhere would prove nothing about scheduling.
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardWithNoTriggersRecomputesEveryTick()
        {
            var machine = SpawnPolledGuard(trigger: null);

            Time.captureDeltaTime = FrameSeconds;

            var guard = RunningNode<ReactiveGuard>(machine);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            int start = guard.Evaluations;

            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.That(guard.Evaluations - start, Is.InRange(8, 12),
                "An empty trigger list means always due, so this is the per-frame cost the other schedules "
                + "are being compared against.");
        }

        /// <summary>
        /// A guard on a node that is re-entered every frame recomputes every frame, whatever its interval
        /// says.
        ///
        /// <para>
        /// <b>Characterization, not a requirement.</b> This documents an interaction rather than asserting
        /// that it is correct, because whether it is correct has not been decided — see
        /// <see href="https://github.com/platinio/Unity-BH3/issues/16">issue 16</see>. It is here so the
        /// behaviour is measured rather than discovered later by someone wondering why a guard with a
        /// ten-second interval is the most expensive thing in their profile.
        /// </para>
        ///
        /// <para>
        /// The mechanism is a collision between two reasonable rules. Entry always evaluates a guard —
        /// <c>Ask(fresh: true)</c> skips the <c>IsDue</c> check outright, deliberately, because a stale
        /// <c>true</c> admits a branch whose precondition no longer holds. And <c>Repeater</c> exits and
        /// re-enters its child the moment the child completes. Put a guard on a node that completes each
        /// frame, and the container above sets the guard's evaluation rate — the trigger it declares is
        /// bypassed entirely, from outside, with nothing local to explain it.
        /// </para>
        ///
        /// <para>
        /// The contrast with <see cref="AnIntervalTriggerRecomputesWhenTheIntervalElapses"/> is the point:
        /// same guard, same kind of trigger, and the only difference is whether the owner is re-entered or
        /// merely polled.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardOnANodeReEnteredEveryFrameIgnoresItsInterval()
        {
            var tree = NewTree();
            var graph = tree.graph;

            // Wait(0) completes on the tick it runs, so the Repeater exits and re-enters it every frame.
            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var cycling = Add<WaitTime>(graph, 0.0f, 250.0f);
            FeedFloat(graph, cycling, cycling.Time, 0.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, cycling);

            var read = ReadAgentVariable(graph, "eligible", -400.0f, 250.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -150.0f, 250.0f);
            guard.UpdateOwner(cycling);
            read.Value.ValidlyConnectTo(guard.Value);

            // Ten seconds. Against a 0.05s frame that is 200 frames per fire, so a guard whose schedule were
            // being honoured would not recompute once across the run below.
            guard.AddTrigger(GuardTrigger.Interval(10.0f));

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("eligible", true));

            Time.captureDeltaTime = FrameSeconds;

            var running = RunningNode<ReactiveGuard>(machine);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            int start = running.Evaluations;

            const int frames = 20;

            for (int frame = 0; frame < frames; frame++)
            {
                yield return null;
            }

            int evaluations = running.Evaluations - start;

            Assert.Greater(evaluations, frames / 2,
                $"A 10s interval over {frames} frames of 0.05s should schedule zero recomputes; saw "
                + $"{evaluations}. Entry evaluation is what is being counted, and the Repeater above supplies "
                + "one entry per frame. Recorded so the cost is visible: if issue 16 is resolved by changing "
                + "when a Repeater re-enters, this is the test that will say so.");
        }

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ guarded branch, a branch that runs forever ].
        ///
        /// <para>
        /// The guard answers false, so its branch is never entered and the Selector settles on the branch
        /// below. That is the arrangement being measured: a Selector resumes at its running child rather than
        /// re-checking the siblings above it, so the guarded branch is reached only through the preemption
        /// scan — which asks with <c>fresh: false</c>, the one path a trigger actually gates. Were the branch
        /// entered and re-entered instead, entry would recompute every time regardless of schedule and the
        /// count would measure nothing.
        /// </para>
        /// </summary>
        private BehaviorTreeMachine SpawnPolledGuard(GuardTrigger trigger)
        {
            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var guarded = Add<WaitTime>(graph, -400.0f, 400.0f);
            FeedFloat(graph, guarded, guarded.Time, 999.0f);

            var fallback = Add<WaitTime>(graph, 400.0f, 400.0f);
            FeedFloat(graph, fallback, fallback.Time, 999.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, guarded);
            Connect(graph, selector, fallback);

            var read = ReadAgentVariable(graph, "canAttack", -700.0f, 250.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 250.0f);
            guard.UpdateOwner(guarded);
            read.Value.ValidlyConnectTo(guard.Value);

            if (trigger != null) guard.AddTrigger(trigger);

            return Spawn(tree, (_, variables) => variables.declarations.Set("canAttack", false));
        }
    }
}

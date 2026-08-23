using System.Collections;
using System.Linq;
using System.Reflection;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Reactive guards as three units wired together, rather than as three units.
    ///
    /// <para>
    /// The edit-mode suite pins guard <em>semantics</em> thoroughly — preemption, trigger OR-ing, the
    /// entry-always-recomputes rule — and it pins <see cref="AgentVariableWriter"/>'s version counting. What it
    /// cannot reach is the seam between them, and the reason is structural rather than an oversight:
    /// <c>GuardTrigger.WriterFor</c> resolves the writer through <c>owner.gameObject</c>, which is
    /// <c>machine.gameObject</c>, and an edit-mode graph has no machine. The lookup throws, the catch returns
    /// null, and every key trigger in edit mode is permanently unable to wake. So the whole
    /// <see cref="GuardTriggerKind.OnKeyChanged"/> path — the default kind, and the one
    /// <c>BehaviorTreeAuthoring.GuardOnVariable</c> seeds automatically — is only observable with a machine
    /// ticking in a real player loop.
    /// </para>
    ///
    /// <para>
    /// That is the whole reason these are play-mode tests and not more unit tests: nothing here is a new rule
    /// about guards. Every rule involved is already covered. What is not covered is that the pieces are
    /// connected, and connection is exactly what a unit test dissolves.
    /// </para>
    /// </summary>
    public class ReactiveGuardIntegrationTests
    {
        private GameObject agent;
        private FunctionGraphAsset function;

        /// <summary>
        /// Agents beyond the first. A cost comparison needs two of them alive at once, seeing the same frames,
        /// or the two halves are measured against different clocks.
        /// </summary>
        private readonly System.Collections.Generic.List<GameObject> extraAgents = new();

        [TearDown]
        public void TearDown()
        {
            if (agent != null) Object.DestroyImmediate(agent);

            foreach (var extra in extraAgents)
            {
                if (extra != null) Object.DestroyImmediate(extra);
            }

            extraAgents.Clear();

            if (function != null) Object.DestroyImmediate(function);

            FunctionEvaluator.InvalidateAll();
            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        /// <summary>
        /// A guard watching a key wakes when a sensor writes that key — on an agent that did not already
        /// carry an <see cref="AgentVariableWriter"/>.
        ///
        /// <para>
        /// The missing-writer start is the point, not incidental setup. <c>AgentVariableWriter</c> is
        /// get-or-add on every write path precisely so an agent prefab that predates the component still
        /// works, and <c>GuardTrigger.WriterFor</c> documents that intent directly: <i>"the component appears
        /// as soon as anything publishes a fact, because every write path is get-or-add."</i> An agent whose
        /// sensors publish on first detection rather than in <c>Awake</c> — the ordinary case — reaches its
        /// first guard evaluation before the component exists.
        /// </para>
        ///
        /// <para>
        /// Every shipped demo sensor carries <c>[RequireComponent(typeof(AgentVariableWriter))]</c>, so the
        /// component is there from scene load and the demos never exercise this ordering. That is why running
        /// an example and watching it work does not cover it.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardWatchingAKeyWakesWhenASensorFirstWritesIt()
        {
            var tree = BuildGuardedTree(out var attack, out var idle, out var attackGuard);
            var machine = SpawnAgent(tree, hasTarget: false);

            Assert.IsFalse(machine.gameObject.TryGetComponent<AgentVariableWriter>(out _),
                "The fixture must start without a writer -- that is the ordering under test. A sensor that "
                + "publishes on first detection creates it later, and get-or-add is what makes that legal.");

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;

            // The trigger has to have survived Instantiate, or a pass here would mean nothing: a guard whose
            // trigger list arrived empty is "always due", which wakes for the opposite reason to the one
            // under test.
            var runningGuard = RunningGuard(machine);
            Assert.AreEqual(1, runningGuard.Triggers.Count,
                "The guard's trigger must survive the machine's Instantiate of the macro, or this test passes "
                + "by being unscheduled rather than by waking.");
            Assert.AreEqual(GuardTriggerKind.OnKeyChanged, runningGuard.Triggers[0].Kind);
            CollectionAssert.Contains(runningGuard.Triggers[0].Keys, "hasTarget");

            Assert.IsTrue(Entered(recorder, idle), "With no target, Idle is the branch that holds.");
            Assert.IsFalse(Entered(recorder, attack), "and Attack has not started.");

            // A sensor spots something and publishes through the writer, which is get-or-add: this call is
            // what puts the component on the agent for the first time.
            bool versioned = AgentVariableWriter.SetOn(machine.gameObject, "hasTarget", true);

            Assert.IsTrue(versioned, "An agent runs a tree, so the write is versioned rather than plain.");
            Assert.AreEqual(1, AgentVariableWriter.On(machine.gameObject).VersionOf("hasTarget"),
                "and the version moved, so there is something for a watching guard to notice.");

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(Entered(recorder, attack),
                "Attack's guard watches hasTarget and the fact moved, so Attack must take the slot. If this "
                + "fails, the guard never re-evaluated: WriterFor caches its resolved writer against the agent "
                + "on first use, and a null resolved before the component existed is cached just as firmly as "
                + "a real one -- so the guard is permanently unable to see any later write.");

            Assert.IsTrue(
                recorder.Events.Any(e => e.Kind == BehaviorTreeEventKind.NodeTakenOver && e.NodeGuid == idle),
                "and Idle lost the slot to it, rather than Attack starting after Idle happened to finish.");
        }

        /// <summary>
        /// The same agent, but with the writer present from the start — which is what every shipped demo
        /// looks like, because each demo sensor declares <c>[RequireComponent]</c> for it.
        ///
        /// <para>
        /// Paired with the test above deliberately. Alone, either one is ambiguous: this one passing says
        /// nothing about ordering, and the other one failing could be read as "key triggers do not work". The
        /// two together isolate the variable to <em>when the component appeared</em>, which is what turns a
        /// red test into a diagnosis.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardWakesWhenTheWriterWasAlreadyOnTheAgent()
        {
            var tree = BuildGuardedTree(out var attack, out _, out _);
            var machine = SpawnAgent(tree, hasTarget: false, preAttachWriter: true);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;
            Assert.IsFalse(Entered(recorder, attack), "Attack has not started while the fact is false.");

            AgentVariableWriter.SetOn(machine.gameObject, "hasTarget", true);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(Entered(recorder, attack),
                "With the writer present before the first evaluation, the guard resolves a real writer and "
                + "wakes on the version bump.");
        }

        /// <summary>
        /// A guard whose condition is a Function wakes on the key that Function <em>declares</em> — with a
        /// trigger that names no keys of its own.
        ///
        /// <para>
        /// The empty key list is the whole test. A trigger with no usable keys watches nothing and can never
        /// become due, so before inheritance existed this arrangement produced a guard that never woke: the
        /// silent failure the watched-keys declaration was added to prevent and, until now, did not. If
        /// inheritance stops working this test fails by Attack never being entered, which is exactly how the
        /// bug presents in a real tree.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardReadingAFunction_WakesOnTheKeyThatFunctionDeclares()
        {
            var tree = BuildFunctionGuardedTree(out var attack, out var idle);
            var machine = SpawnAgent(tree, hasTarget: false, preAttachWriter: true);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            var recorder = machine.FlightRecorder;
            var runningGuard = RunningGuard(machine);
            var trigger = runningGuard.Triggers[0];

            Assert.AreEqual(GuardTriggerKind.OnKeyChanged, trigger.Kind);
            Assert.AreEqual(0, trigger.UsableKeyCount(),
                "The authored trigger must name no keys, or this test could pass on a hand-typed key rather "
                + "than on the inherited one.");
            CollectionAssert.Contains(trigger.InheritedKeys, "hasTarget",
                "The guard must have picked the key up from the Function its condition reads.");

            Assert.IsTrue(Entered(recorder, idle), "With no target, Idle is the branch that holds.");
            Assert.IsFalse(Entered(recorder, attack), "and Attack has not started.");

            Assert.IsTrue(AgentVariableWriter.SetOn(machine.gameObject, "hasTarget", true),
                "An agent runs a tree, so the write is versioned rather than plain.");

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(Entered(recorder, attack),
                "The Function declares 'hasTarget', so a guard reading that Function must wake when it moves "
                + "-- even though nobody typed that key onto the guard.");
        }

        /// <summary>
        /// The same guard stays asleep when an unrelated fact moves.
        ///
        /// <para>
        /// Without this, the test above would also pass for a guard that had simply become always-due —
        /// which is the failure mode of inheriting nothing, since a trigger list that watches nothing is
        /// indistinguishable from no schedule at all if you only ever check that the guard woke.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AGuardReadingAFunction_StaysAsleepWhenAnUnrelatedFactMoves()
        {
            var tree = BuildFunctionGuardedTree(out _, out _);
            var machine = SpawnAgent(tree, hasTarget: false, preAttachWriter: true);

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            var runningGuard = RunningGuard(machine);
            int before = runningGuard.Evaluations;

            Assert.IsTrue(AgentVariableWriter.SetOn(machine.gameObject, "noise", 1.0f),
                "The unrelated write must still be versioned, or nothing is being gated.");

            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(before, runningGuard.Evaluations,
                "'noise' is not declared by the Function, so it must not make the guard recompute. A guard "
                + "that re-runs on every write has inherited nothing and is merely always due.");
        }

        /// <summary>
        /// The demo's headline claim, as an assertion: an inherited schedule reaches the <em>same decision</em>
        /// as no schedule at all, for a fraction of the graph runs.
        ///
        /// <para>
        /// This is the third variant of the demo scene — the control it compares against — and until now it
        /// was verified only by reading numbers off a screenshot. That is exactly the kind of claim that rots
        /// without anyone noticing: inheritance could quietly stop working and the two agents would still
        /// agree on the branch, because a guard that has become permanently due is <em>correct</em>. It is
        /// only wrong about cost, and cost is invisible unless something counts it.
        /// </para>
        ///
        /// <para>
        /// Both agents run the same tree asset and see the same frames, so the comparison is between
        /// schedules rather than between clocks. The bound is deliberately loose — an order of magnitude,
        /// not an exact count — because the point is the shape of the difference and a tight number would
        /// fail on an unrelated timing change without telling anyone anything.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator AnInheritedSchedule_ReachesTheSameDecisionForAFractionOfTheGraphRuns()
        {
            const int Frames = 40;

            var tree = BuildFunctionGuardedTree(out var attack, out _);

            var scheduled = SpawnAgent(tree, hasTarget: false, preAttachWriter: true);
            var everyTick = SpawnExtraAgent(tree);

            // The control: same condition, same Function, no schedule at all.
            RunningGuard(everyTick).ClearTriggers();

            for (var frame = 0; frame < Frames; frame++)
            {
                // One state change, halfway, so both agents have something to react to and the counts either
                // side of it are comparable.
                if (frame == Frames / 2)
                {
                    AgentVariableWriter.SetOn(scheduled.gameObject, "hasTarget", true);
                    AgentVariableWriter.SetOn(everyTick.gameObject, "hasTarget", true);
                }

                yield return null;
            }

            var scheduledRuns = RunningGuard(scheduled).Evaluations;
            var everyTickRuns = RunningGuard(everyTick).Evaluations;

            Assert.IsTrue(Entered(scheduled.FlightRecorder, attack),
                "The inheriting guard must reach the same decision -- a cheaper schedule that changes the "
                + "answer is not an optimisation, it is a bug.");
            Assert.IsTrue(Entered(everyTick.FlightRecorder, attack),
                "and so must the control, or the two are not comparable.");

            Assert.That(everyTickRuns, Is.GreaterThan(Frames / 2),
                "The control must actually be paying every tick, or there is no baseline to be cheaper than.");

            Assert.That(scheduledRuns, Is.LessThan(everyTickRuns / 4),
                $"An inherited schedule must cost a fraction of no schedule: {scheduledRuns} runs against "
                + $"{everyTickRuns} over {Frames} frames. If these converge, inheritance has stopped gating "
                + "and the guard is merely always due -- which still produces the right answer, and is the "
                + "reason this needs its own test rather than being covered by the wake tests.");
        }

        #region Scenario

        /// <summary>
        /// Entry -> Repeater -> Selector -> [ Attack, Idle ]. Attack carries a
        /// <see cref="BooleanReactiveGuard"/> on <c>hasTarget</c> with an <see cref="GuardTriggerKind.OnKeyChanged"/>
        /// trigger naming that key — the shape <c>GuardOnVariable</c> produces.
        /// <para>
        /// Idle carries no guard at all, which is the arrangement the whole preemption feature exists for: the
        /// branch that wants to take over carries the condition, and the fallback carries none.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset BuildGuardedTree(
            out System.Guid attack, out System.Guid idle, out System.Guid attackGuard)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var attackNode = Add<WaitTime>(graph, -900.0f, 400.0f);
            var idleNode = Add<WaitTime>(graph, 900.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attackNode);
            Connect(graph, selector, idleNode);

            // WaitTime.Time declares no default, so it must be connected. Long enough that nothing here ever
            // ends on its own -- the only thing that can change which branch runs is a guard.
            FeedFloat(graph, attackNode, attackNode.Time, 999.0f);
            FeedFloat(graph, idleNode, idleNode.Time, 999.0f);

            var key = Add<StringLiteral>(graph, -600.0f, 0.0f);
            SetPrivateField(key, "value", "hasTarget");

            var read = Add<GetVariable>(graph, -400.0f, 0.0f);
            SetPrivateField(read, "VariableKind", VariableKind.Object);
            key.Value.ValidlyConnectTo(read.Key);

            var guard = Add<BooleanReactiveGuard>(graph, -900.0f, 250.0f);
            guard.UpdateOwner(attackNode);
            read.Value.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged("hasTarget"));

            attack = attackNode.guid;
            idle = idleNode.guid;
            attackGuard = guard.guid;

            return asset;
        }

        /// <summary>
        /// A predicate Function that returns the agent's <c>hasTarget</c> and declares that it reads it.
        /// <para>
        /// Built in memory rather than on disk: this assembly is a runtime one and cannot reach
        /// <c>AssetDatabase</c>. Nothing in the evaluation seam needs a persisted asset — the binding plan
        /// reads the graph, which exists either way.
        /// </para>
        /// </summary>
        private FunctionGraphAsset HasTargetFunction()
        {
            function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = function.graph;

            var input = new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) };
            var output = new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) };
            graph.units.Add(input);
            graph.units.Add(output);

            // Fully qualified: inside ArcaneOnyx.BehaviorTree.* these names resolve to BH3's own port
            // definition types, which are unrelated to Visual Scripting's despite the identical spelling.
            graph.controlInputDefinitions.Add(new Unity.VisualScripting.ControlInputDefinition
            {
                key = FunctionGraphAsset.EnterKey, label = FunctionGraphAsset.EnterKey
            });
            graph.controlOutputDefinitions.Add(new Unity.VisualScripting.ControlOutputDefinition
            {
                key = FunctionGraphAsset.ExitKey, label = FunctionGraphAsset.ExitKey
            });
            graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
            {
                key = FunctionGraphAsset.ResultKey,
                label = FunctionGraphAsset.ResultKey,
                type = typeof(bool)
            });

            graph.PortDefinitionsChanged();
            input.controlOutputs[FunctionGraphAsset.EnterKey]
                .ValidlyConnectTo(output.controlInputs[FunctionGraphAsset.ExitKey]);

            // A fallback, so an agent that has not declared the key yet reads false instead of throwing --
            // the same shape CreateVariableReadFunction produces.
            var read = new Unity.VisualScripting.GetVariable
            {
                kind = VariableKind.Object,
                specifyFallback = true,
                position = new Vector2(-160.0f, 0.0f)
            };
            graph.units.Add(read);
            read.name.SetDefaultValue("hasTarget");

            var fallback = new Unity.VisualScripting.Literal(typeof(bool), false)
            {
                position = new Vector2(-320.0f, 120.0f)
            };
            graph.units.Add(fallback);
            fallback.output.ValidlyConnectTo(read.fallback);

            read.value.ValidlyConnectTo(output.valueInputs[FunctionGraphAsset.ResultKey]);

            function.SetWatchedKeys(new[] { "hasTarget" });

            return function;
        }

        /// <summary>
        /// The <see cref="BuildGuardedTree"/> scenario with one substitution: the guard's condition is a
        /// Function rather than a bare variable read, and its trigger names <b>no keys at all</b>. Everything
        /// that makes the guard wake therefore has to arrive from the Function.
        /// </summary>
        private BehaviorTreeGraphAsset BuildFunctionGuardedTree(out System.Guid attack, out System.Guid idle)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();
            var graph = asset.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var selector = Add<Selector>(graph, 0.0f, 250.0f);

            var attackNode = Add<WaitTime>(graph, -900.0f, 400.0f);
            var idleNode = Add<WaitTime>(graph, 900.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, selector);
            Connect(graph, selector, attackNode);
            Connect(graph, selector, idleNode);

            FeedFloat(graph, attackNode, attackNode.Time, 999.0f);
            FeedFloat(graph, idleNode, idleNode.Time, 999.0f);

            var read = Add<VisualScriptGraphVariable>(graph, -600.0f, 0.0f);
            read.SetFunction(HasTargetFunction());

            var guard = Add<BooleanReactiveGuard>(graph, -900.0f, 250.0f);
            guard.UpdateOwner(attackNode);
            read.Output.ValidlyConnectTo(guard.Value);
            guard.AddTrigger(GuardTrigger.KeyChanged());

            attack = attackNode.guid;
            idle = idleNode.guid;

            return asset;
        }

        /// <summary>
        /// A second agent on the same tree, tracked separately so the fixture still tears everything down.
        /// <para>
        /// Its own machine instantiates its own clone of the macro, so the two agents share an asset and
        /// nothing else — which is the whole point when the thing under test is a per-agent schedule.
        /// </para>
        /// </summary>
        private BehaviorTreeMachine SpawnExtraAgent(BehaviorTreeGraphAsset tree)
        {
            var extra = new GameObject("Zombie (control)");
            extra.SetActive(false);
            extraAgents.Add(extra);

            var machine = extra.AddComponent<BehaviorTreeMachine>();
            extra.GetComponent<Variables>().declarations.Set("hasTarget", false);
            extra.AddComponent<AgentVariableWriter>();

            machine.nest.macro = tree;
            extra.SetActive(true);

            return machine;
        }

        /// <summary>
        /// Stands the agent up the way a prefab would: the machine reads its macro in <c>Awake</c>, so the
        /// object is built inactive and switched on once everything it needs is in place.
        /// </summary>
        private BehaviorTreeMachine SpawnAgent(
            BehaviorTreeGraphAsset tree, bool hasTarget, bool preAttachWriter = false)
        {
            agent = new GameObject("Zombie");
            agent.SetActive(false);

            var machine = agent.AddComponent<BehaviorTreeMachine>();
            var variables = agent.GetComponent<Variables>();
            variables.declarations.Set("hasTarget", hasTarget);

            if (preAttachWriter) agent.AddComponent<AgentVariableWriter>();

            machine.nest.macro = tree;
            agent.SetActive(true);

            return machine;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The guard on the graph the machine is actually running.
        /// <para>
        /// Not the one <see cref="BuildGuardedTree"/> authored: <c>Machine.Awake</c> does
        /// <c>Instantiate(nest.macro)</c>, so every node that runs is a clone. Anything asserted about
        /// scheduling has to be read off the clone or it is asserting about a node nothing ticks.
        /// </para>
        /// </summary>
        private static ReactiveGuard RunningGuard(BehaviorTreeMachine machine)
        {
            var guard = machine.RunningGraph.Nodes.OfType<ReactiveGuard>().FirstOrDefault();
            Assert.IsNotNull(guard, "The running graph must contain the reactive guard.");

            return guard;
        }

        private static bool Entered(BehaviorTreeFlightRecorder recorder, System.Guid node)
        {
            return recorder.Events.Any(e => e.Kind == BehaviorTreeEventKind.NodeEnter && e.NodeGuid == node);
        }

        private static T Add<T>(BehaviorTreeGraph graph, float x, float y) where T : BehaviorTreeNode, new()
        {
            var node = new T();
            node.Position = new Rect(new Vector2(x, y), node.StartingSize);
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, graph.CountTransitionsFromNode(parent));
            graph.Transitions.Add(transition);
        }

        private static void FeedFloat(BehaviorTreeGraph graph, BehaviorTreeNode owner, ValueInput port, float value)
        {
            var literal = Add<FloatLiteral>(graph, owner.Position.x, owner.Position.y + 150.0f);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        /// <summary>
        /// Literal values and a Get Variable's kind are private serialized fields with no setter — the
        /// inspector writes them through SerializedProperty, and authoring code through reflection.
        /// </summary>
        private static void SetPrivateField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no field '{field}'.");

            info.SetValue(target, value);
        }

        #endregion
    }
}

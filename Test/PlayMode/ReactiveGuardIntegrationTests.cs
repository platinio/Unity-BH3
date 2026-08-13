using System.Collections;
using System.Linq;
using System.Reflection;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
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

        [TearDown]
        public void TearDown()
        {
            if (agent != null) Object.DestroyImmediate(agent);

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
            var guard = machine.GraphInstance.graph.Nodes.OfType<ReactiveGuard>().FirstOrDefault();
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

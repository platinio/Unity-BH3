using System;
using System.Linq;
using System.Reflection;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Shared scaffolding for standing a real agent up and reading what it did.
    ///
    /// <para>
    /// The play-mode assembly cannot reference the edit-mode one, and it cannot reference
    /// <c>ArcaneOnyx.BehaviorTree.Editor</c> either — so <c>BehaviorTreeAuthoring</c> and its guard rails are
    /// unavailable here and every tree has to be assembled through the raw API. That is a lot of ceremony to
    /// repeat per fixture, and repeating it is how two tests end up quietly building different trees.
    /// </para>
    ///
    /// <para>
    /// <b>Assert through the flight recorder, not through node references.</b> <c>Machine.Awake</c> does
    /// <c>Instantiate(nest.macro)</c>, so every node that actually runs is a <em>clone</em> of the one the
    /// fixture authored. A test holding the authored node is watching an object nothing ticks. Guids survive
    /// the clone, which is why the recorder — keyed by guid — is the honest observation surface, and why
    /// <see cref="RunningNode{T}"/> exists for the cases that genuinely need the live instance.
    /// </para>
    /// </summary>
    public abstract class PlayModeAgentFixture
    {
        /// <summary>The agent under test. Destroyed after every test, so a fixture never leaks into the next.</summary>
        protected GameObject Agent { get; private set; }

        [TearDown]
        public void DestroyAgentAndResetRecorders()
        {
            if (Agent != null) UnityEngine.Object.DestroyImmediate(Agent);

            Agent = null;

            BehaviorTreeFlightRecorders.Reset();
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
        }

        /// <summary>
        /// Stands the agent up the way a prefab would. The machine reads its macro in <c>Awake</c>, so the
        /// object is built inactive and switched on only once everything it needs is in place —
        /// <paramref name="configure"/> runs in that window, which is the only chance to declare variables or
        /// attach components before the tree's first tick.
        /// </summary>
        protected BehaviorTreeMachine Spawn(
            BehaviorTreeGraphAsset tree, Action<GameObject, Variables> configure = null)
        {
            Agent = new GameObject("Agent");
            Agent.SetActive(false);

            var machine = Agent.AddComponent<BehaviorTreeMachine>();
            var variables = Agent.GetComponent<Variables>();

            configure?.Invoke(Agent, variables);

            machine.nest.macro = tree;
            Agent.SetActive(true);

            return machine;
        }

        protected static BehaviorTreeGraphAsset NewTree() =>
            ScriptableObject.CreateInstance<BehaviorTreeGraphAsset>();

        /// <summary>Adds a node sized from its own <c>StartingSize</c>, the way the create menu does.</summary>
        protected static T Add<T>(BehaviorTreeGraph graph, float x, float y) where T : BehaviorTreeNode, new()
        {
            var node = new T();
            node.Position = new Rect(new Vector2(x, y), node.StartingSize);
            graph.Nodes.Add(node);

            return node;
        }

        /// <summary>
        /// Parent -> child. The transition's stored index is the priority, and it is taken from how many
        /// transitions the parent already has — so making the calls in the order the branches should be tried
        /// produces the right tree.
        /// </summary>
        protected static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, graph.CountTransitionsFromNode(parent));
            graph.Transitions.Add(transition);
        }

        /// <summary>
        /// Connects a literal to a port. This is the only way to give a bare port a value that survives the
        /// asset round trip: <c>SetDefaultValue</c> writes a key that <c>Definition()</c> will not rebuild, so
        /// it is gone the moment the machine instantiates the macro.
        /// </summary>
        protected static void FeedFloat(BehaviorTreeGraph graph, BehaviorTreeNode near, ValueInput port, float value)
        {
            var literal = Add<FloatLiteral>(graph, near.Position.x, near.Position.y + 150.0f);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        protected static void FeedBool(BehaviorTreeGraph graph, BehaviorTreeNode near, ValueInput port, bool value)
        {
            var literal = Add<BoolLiteral>(graph, near.Position.x, near.Position.y + 150.0f);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        protected static void FeedString(BehaviorTreeGraph graph, BehaviorTreeNode near, ValueInput port, string value)
        {
            var literal = Add<StringLiteral>(graph, near.Position.x, near.Position.y + 150.0f);
            SetPrivateField(literal, "value", value);
            literal.Value.ValidlyConnectTo(port);
        }

        /// <summary>
        /// A Visual Scripting read of an agent variable, as a value source for a guard.
        /// </summary>
        protected static GetVariable ReadAgentVariable(BehaviorTreeGraph graph, string key, float x, float y)
        {
            var keyLiteral = Add<StringLiteral>(graph, x - 200.0f, y);
            SetPrivateField(keyLiteral, "value", key);

            var read = Add<GetVariable>(graph, x, y);
            SetPrivateField(read, "VariableKind", VariableKind.Object);
            keyLiteral.Value.ValidlyConnectTo(read.Key);

            return read;
        }

        /// <summary>
        /// Literal values and a Get Variable's kind are private serialized fields with no setter — the
        /// inspector writes them through SerializedProperty, and authoring code through reflection. Same thing
        /// the editor-side authoring helpers do.
        /// </summary>
        protected static void SetPrivateField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name} has no field '{field}'.");

            info.SetValue(target, value);
        }

        /// <summary>
        /// The live instance of a node type on the graph the machine is running — not the authored one. Use
        /// only when a test needs runtime state that the recorder does not carry, such as a guard's
        /// evaluation count.
        /// </summary>
        protected static T RunningNode<T>(BehaviorTreeMachine machine) where T : BehaviorTreeNode
        {
            var node = machine.GraphInstance.graph.Nodes.OfType<T>().FirstOrDefault();
            Assert.IsNotNull(node, $"The running graph must contain a {typeof(T).Name}.");

            return node;
        }

        protected static bool Entered(BehaviorTreeFlightRecorder recorder, Guid node)
        {
            AssertNothingDropped(recorder);

            return recorder.Events.Any(e => e.Kind == BehaviorTreeEventKind.NodeEnter && e.NodeGuid == node);
        }

        protected static int EnterCount(BehaviorTreeFlightRecorder recorder, Guid node)
        {
            AssertNothingDropped(recorder);

            return recorder.Events.Count(e => e.Kind == BehaviorTreeEventKind.NodeEnter && e.NodeGuid == node);
        }

        protected static int ExitCount(BehaviorTreeFlightRecorder recorder, Guid node)
        {
            AssertNothingDropped(recorder);

            return recorder.Events.Count(e => e.Kind == BehaviorTreeEventKind.NodeExit && e.NodeGuid == node);
        }

        /// <summary>
        /// Every question this fixture asks the recording is asked of a <em>bounded ring</em>. Once it is
        /// full the oldest event is overwritten and only <c>Dropped</c> says so, which would turn each helper
        /// above into a silent under-count: <see cref="Entered"/> would answer false about something that
        /// happened, and a test asserting a count had not grown would pass while the count was being eaten
        /// from the other end.
        /// <para>
        /// A test that overruns the buffer is a test that needs a bigger buffer or a shorter run, and it
        /// should say so rather than quietly measure the wrong thing.
        /// </para>
        /// </summary>
        private static void AssertNothingDropped(BehaviorTreeFlightRecorder recorder)
        {
            Assert.AreEqual(0, recorder.Dropped,
                $"The recording overflowed its ring and lost {recorder.Dropped} events, so anything counted "
                + "from it is a lower bound rather than an answer. Shorten the run or raise the capacity.");
        }
    }
}

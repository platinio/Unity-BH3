using System.Collections;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// Swapping an agent's tree at runtime, and what an agent leaves behind when it dies.
    ///
    /// <para>
    /// <c>Switch</c> is the only supported way to change what an agent is running without rebuilding it —
    /// a boss changing phase, an NPC entering combat, a possessed unit handed a different brain. It repeats
    /// a subset of what <c>Awake</c> does, and the interesting question is which subset: <c>Awake</c>
    /// instantiates the macro, checks recursion, binds the recorder, builds the root scope and calls
    /// <c>OnAwake</c>, and any of those left out is a capability that silently stops working after a switch
    /// rather than failing.
    /// </para>
    ///
    /// <para>
    /// Teardown matters for the same reason in reverse. An agent that dies mid-branch has an instantiated
    /// graph, possibly instantiated sub-trees under it, a recorder in a global registry and a scope chain. A
    /// leak there is invisible until a wave-based scene has spawned a few hundred agents.
    /// </para>
    /// </summary>
    public class MachineSwitchAndTeardownTests : PlayModeAgentFixture
    {
        /// <summary>The branch a guard should refuse, and the fallback that should run instead.</summary>
        private System.Guid guarded;
        private System.Guid fallback;

        /// <summary>
        /// Switching runs the new tree. The baseline the other two are measured against — if this failed,
        /// nothing below would mean anything.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingRunsTheNewTree()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);

            yield return Frames(2);

            var replacement = BuildGuardedTree(guardAnswers: true);
            var replacementBranch = guarded;

            machine.Switch(replacement);

            yield return Frames(3);

            Assert.IsTrue(Entered(machine.FlightRecorder, replacementBranch),
                "After a switch the machine has to be running the tree it was handed.");
        }

        /// <summary>
        /// A switched-in tree's guards have to work.
        ///
        /// <para>
        /// Guards are not parented to the nodes they protect — a guard names an owner and the runtime attaches
        /// it during <c>OnAwake</c>, so a guard is inert until the graph has been awoken. <c>Awake</c> calls
        /// <c>behaviorTreeGraph.OnAwake()</c>; <c>Switch</c> does not. If that omission matters, the guarded
        /// branch here runs despite a guard that answers false, and the failure is silent: no exception, no
        /// warning, just an agent that ignores every precondition in the tree it was just given.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ASwitchedTreeStillHonoursItsGuards()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);

            yield return Frames(2);

            // The replacement's guarded branch must be refused: its guard reads a fact that is false.
            var replacement = BuildGuardedTree(guardAnswers: false);
            var replacementGuarded = guarded;
            var replacementFallback = fallback;

            machine.Switch(replacement);

            yield return Frames(3);

            var recorder = machine.FlightRecorder;

            Assert.IsFalse(Entered(recorder, replacementGuarded),
                "The guard on this branch answers false, so the branch must not run. If it did, the guard was "
                + "never attached to its owner -- guards are wired during graph OnAwake, and Switch does not "
                + "call it. Every precondition in a switched-in tree would be ignored, silently.");

            Assert.IsTrue(Entered(recorder, replacementFallback),
                "and the fallback below it takes the slot instead.");
        }

        /// <summary>
        /// A switched-in tree has to be run as a private instance, the way <c>Awake</c> runs the macro.
        ///
        /// <para>
        /// <c>Awake</c> does <c>Instantiate(nest.macro)</c> so each agent runs its own copy; that is what
        /// makes one asset safe on many agents. <c>Switch</c> assigns the asset straight to
        /// <c>graphInstance</c>. Three consequences if that stands: two agents switched to the same tree
        /// share every node's runtime state; the asset accumulates runtime state, which in the editor dirties
        /// the file on disk; and <c>OnDestroy</c> calls <c>Destroy(graphInstance)</c>, which is then aimed at
        /// the asset rather than at a copy.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ASwitchedTreeRunsAPrivateInstanceRatherThanTheAssetItself()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);

            yield return Frames(2);

            var replacement = BuildGuardedTree(guardAnswers: true);

            machine.Switch(replacement);

            yield return Frames(2);

            Assert.AreNotSame(replacement, machine.GraphInstance,
                "A switched-in tree must be instantiated, not run in place. Sharing the asset means two "
                + "agents switched to it share node state, the asset collects runtime state, and OnDestroy's "
                + "Destroy(graphInstance) is pointed at the asset.");
        }

        /// <summary>
        /// A destroyed agent releases the graph it instantiated. The instance is created per agent, so
        /// nothing else can free it.
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingAnAgentReleasesTheGraphItInstantiated()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);

            yield return Frames(2);

            var instance = machine.GraphInstance;

            Assert.IsFalse(instance == null, "The agent is running an instantiated graph.");

            Object.DestroyImmediate(Agent);

            yield return null;

            // Unity's overloaded equality: a destroyed object compares equal to null while the managed
            // reference still exists, so this is the only way to ask.
            Assert.IsTrue(instance == null,
                "OnDestroy has to destroy the graph it instantiated. One leaked ScriptableObject per agent is "
                + "invisible until a wave-based scene has spawned a few hundred of them.");
        }

        /// <summary>
        /// A destroyed agent releases the sub-tree instances its call sites created, not just the root graph.
        ///
        /// <para>
        /// <c>RunBehaviorTreeGraphNode</c> does <c>Object.Instantiate(behaviorTreeGraphAsset)</c> so each call
        /// site runs a private copy — that is what keeps two call sites of one branch from sharing state, and
        /// it is load-bearing. But the machine's <c>OnDestroy</c> only destroys the graph <em>it</em>
        /// instantiated, and while <c>BehaviorTreeGraph.OnDestroy</c> cascades <c>node.OnDestroy()</c> to
        /// every node, <c>RunBehaviorTreeGraphNode</c> does not override it. Nothing frees the clone.
        /// </para>
        ///
        /// <para>
        /// The cost is one leaked <c>ScriptableObject</c> per call site per agent, multiplying with nesting,
        /// and it lands hardest on exactly the trees this tool encourages — the modular ones. A single agent
        /// never shows it; a wave-based scene spawning modular agents accumulates it for the session.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingAnAgentReleasesTheSubTreeInstancesItsCallSitesCreated()
        {
            var machine = Spawn(BuildTreeWithSubTree(), Declare);

            yield return Frames(3);

            var call = RunningNode<RunBehaviorTreeGraphNode>(machine);

            Assert.IsTrue(call.HasBehaviorTreeGraphInstance,
                "The call site instantiated its branch, which is the object this test is about.");

            // Captured before the agent dies: the property's getter instantiates on demand, so reading it
            // afterwards would quietly manufacture a fresh one and report success.
            var subTreeInstance = call.BehaviorTreeGraphAssetInstance;

            Assert.IsFalse(subTreeInstance == null, "and it is alive while the agent is.");

            Object.DestroyImmediate(Agent);

            yield return null;

            Assert.IsTrue(subTreeInstance == null,
                "The agent is gone, so the copies its call sites made must go with it. Nothing destroys them "
                + "today: the machine frees only its own root instance, and RunBehaviorTreeGraphNode does not "
                + "override OnDestroy, so every sub-tree clone outlives the agent that created it.");
        }

        #region Fixture

        /// <summary>
        /// Entry -&gt; Repeater -&gt; Selector -&gt; [ guarded, fallback ], where the guard reads
        /// <c>eligible</c>. Both branches run forever, so the guard is the only thing that decides.
        /// <para>
        /// <paramref name="guardAnswers"/> picks which fact the guard reads rather than the fact's value,
        /// so one agent can hold both a tree whose guard passes and a tree whose guard refuses.
        /// </para>
        /// </summary>
        private BehaviorTreeGraphAsset BuildGuardedTree(bool guardAnswers)
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

            var read = ReadAgentVariable(graph, guardAnswers ? "alwaysTrue" : "alwaysFalse", -750.0f, 350.0f);

            var guard = Add<BooleanReactiveGuard>(graph, -400.0f, 350.0f);
            guard.UpdateOwner(guardedNode);
            read.Value.ValidlyConnectTo(guard.Value);

            guarded = guardedNode.guid;
            fallback = fallbackNode.guid;

            return tree;
        }

        /// <summary>
        /// Entry -&gt; Repeater -&gt; Run Behavior Tree -&gt; (a branch that holds forever). No parameters: the
        /// question here is only whether the instance the call site makes is ever freed.
        /// </summary>
        private BehaviorTreeGraphAsset BuildTreeWithSubTree()
        {
            var branch = NewTree();
            var hold = Add<WaitTime>(branch.graph, 0.0f, 100.0f);
            FeedFloat(branch.graph, hold, hold.Time, 999.0f);
            Connect(branch.graph, branch.graph.EntryNode, hold);

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var call = Add<RunBehaviorTreeGraphNode>(graph, 0.0f, 250.0f);
            call.SetBehaviorTreeGraphAsset(branch);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, call);

            return tree;
        }

        private static void Declare(GameObject _, Unity.VisualScripting.Variables variables)
        {
            variables.declarations.Set("alwaysTrue", true);
            variables.declarations.Set("alwaysFalse", false);
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

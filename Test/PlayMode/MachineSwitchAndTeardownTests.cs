using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// A dying agent releases sub-tree instances at every depth, not just the first.
        ///
        /// <para>
        /// Each level of nesting clones again, so the leak grew with exactly the modular trees this tool
        /// encourages — and the innermost clone is reachable only <em>through</em> the one above it, which
        /// makes the order of the walk load-bearing rather than incidental. This is the production path for
        /// that: machine destroyed, graph cascades, call site releases, and the same thing happens one level
        /// down inside the clone it was holding.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingAnAgentReleasesNestedSubTreeInstancesAtEveryDepth()
        {
            var machine = Spawn(BuildTreeWithNestedSubTrees(), Declare);

            yield return Frames(3);

            var outerCall = RunningNode<RunBehaviorTreeGraphNode>(machine);

            // Captured before the agent dies: both getters instantiate on demand, so reading either one
            // afterwards would quietly manufacture a fresh clone and report success.
            var outerInstance = outerCall.BehaviorTreeGraphAssetInstance;

            var innerCall = outerCall.BehaviorTreeGraphInstance.Nodes
                .OfType<RunBehaviorTreeGraphNode>().Single();

            var innerInstance = innerCall.BehaviorTreeGraphAssetInstance;

            Assert.IsFalse(outerInstance == null, "both levels are alive while the agent is");
            Assert.IsFalse(innerInstance == null);

            Object.DestroyImmediate(Agent);

            yield return null;

            Assert.IsTrue(outerInstance == null, "the agent's own call site released its branch");
            Assert.IsTrue(innerInstance == null,
                "and the branch released the one nested inside it. Only a walk that goes through the outer "
                + "clone can reach this object at all, so it is the deep trees that leak worst.");
        }

        /// <summary>
        /// The tree a switch replaces is destroyed, not merely dropped.
        ///
        /// <para>
        /// The outgoing graph is an instance the agent made for itself, so nothing else can free it. One leak
        /// per switch sounds small until it is a boss that changes phase on a timer, or a squad whose members
        /// swap between combat and patrol trees all encounter — the count is per swap, not per agent.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingDestroysTheTreeItReplaced()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);

            yield return Frames(2);

            var replaced = machine.GraphInstance;

            Assert.IsFalse(replaced == null, "The agent is running an instance before the switch.");

            machine.Switch(BuildGuardedTree(guardAnswers: true));

            // Destroy is deferred to the end of the frame, so the question cannot be asked in the same one.
            yield return Frames(2);

            Assert.IsTrue(replaced == null,
                "The tree that was switched out has to be destroyed. Nothing else holds a reference to it: "
                + "the machine made it, the machine replaced it, and the machine is the only owner.");
        }

        /// <summary>
        /// A switch stops the outgoing branch rather than abandoning it mid-action.
        ///
        /// <para>
        /// The old tree was running something when it was replaced, and whatever that something started is
        /// still going: a <c>NavMeshAgent</c> walking to a destination, an animation playing, a claim held on
        /// a shared resource. <c>OnDestroy</c> cascades <c>OnDestroy</c> and never <c>OnExit</c>, so only an
        /// explicit exit gives that branch the call it acts on. Asserted through the recorder because the
        /// exit is what the node observes, and the recording is keyed by guid so it survives the clone.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingStopsTheBranchTheOldTreeLeftRunning()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);
            var outgoingBranch = guarded;

            yield return Frames(2);

            var recorder = machine.FlightRecorder;

            Assert.IsTrue(Entered(recorder, outgoingBranch), "The old tree is mid-branch when it is replaced.");
            Assert.AreEqual(0, ExitCount(recorder, outgoingBranch), "and that branch has not stopped yet.");

            machine.Switch(BuildGuardedTree(guardAnswers: true));

            yield return Frames(2);

            Assert.AreEqual(1, ExitCount(machine.FlightRecorder, outgoingBranch),
                "The branch the old tree left running must be exited by the switch. Without it the agent "
                + "keeps walking to a destination chosen by a tree it is no longer running.");
        }

        /// <summary>
        /// Switching revives an agent whose tree had finished.
        ///
        /// <para>
        /// <c>Update</c> stops ticking once the root returns a terminal status, and <c>Switch</c> is the only
        /// thing that can reopen it — the docs advertise exactly that, for a one-shot tree handing over to the
        /// next phase. A switch that left the halted status in place would install the new tree correctly and
        /// then never tick it, which reads from outside as the switch having silently done nothing.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingRevivesAnAgentWhoseTreeHadFinished()
        {
            var machine = Spawn(BuildFinishingTree(), Declare);

            yield return Frames(3);

            Assert.AreEqual(ExecutionStatus.Success, machine.LastExecutionStatus,
                "The one-shot tree has finished, so the machine has stopped ticking.");

            var replacement = BuildGuardedTree(guardAnswers: true);
            var replacementBranch = guarded;

            machine.Switch(replacement);

            yield return Frames(3);

            Assert.IsTrue(Entered(machine.FlightRecorder, replacementBranch),
                "A halted agent handed a new tree has to start running it. Leaving the terminal status in "
                + "place would install the tree and never tick it.");
        }

        /// <summary>
        /// Switching repeatedly keeps exactly one tree alive. The per-swap leak, stated as an accumulation
        /// rather than as a single event.
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingTwiceLeavesOnlyTheNewestTreeAlive()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);

            yield return Frames(2);

            var first = machine.GraphInstance;

            machine.Switch(BuildGuardedTree(guardAnswers: true));

            yield return Frames(2);

            var second = machine.GraphInstance;

            machine.Switch(BuildGuardedTree(guardAnswers: true));

            yield return Frames(2);

            Assert.IsTrue(first == null, "The tree from before the first switch is gone.");
            Assert.IsTrue(second == null, "and so is the one that replaced it.");
            Assert.IsFalse(machine.GraphInstance == null, "leaving only the tree the agent is running now.");
        }

        /// <summary>
        /// A tree switched in before <c>Start</c> is entered once, not twice.
        ///
        /// <para>
        /// Unity runs <c>Awake</c> on activation and <c>Start</c> only later, so a component that calls
        /// <c>Switch</c> from its own <c>Awake</c> — a spawner picking a brain by difficulty, say — installs
        /// the new tree inside that window. If the switch entered the tree itself, the machine's own
        /// <c>Start</c> would then enter it a second time: every node in the live branch re-entered on a tree
        /// that never stopped, which for a leaf means its <c>OnEnter</c> side effect fired twice.
        /// </para>
        ///
        /// <para>
        /// Counted on the entry node because it is the one node the root enter reaches directly, and so the
        /// one place a double entry shows up whatever the tree below it looks like.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ATreeSwitchedInBeforeStartIsEnteredExactlyOnce()
        {
            var machine = Spawn(BuildGuardedTree(guardAnswers: true), Declare);

            // No frame is allowed to pass first: Awake has run, Start has not, and that gap is the case.
            var replacement = BuildGuardedTree(guardAnswers: true);
            var root = replacement.graph.EntryNode.guid;

            machine.Switch(replacement);

            yield return Frames(3);

            Assert.AreEqual(1, EnterCount(machine.FlightRecorder, root),
                "The tree is entered exactly once. Switch entering it and Start entering it again would "
                + "restart a branch that was already running.");
        }

        /// <summary>
        /// A switch that arrives before the machine's own <c>Awake</c> is refused, loudly.
        ///
        /// <para>
        /// Unity does not order <c>Awake</c> between components, so a caller in another component's
        /// <c>Awake</c> can land first. Serving that call is worse than refusing it:
        /// <c>OverrideGraphVariables</c> reads a <c>Variables</c> component that <c>Awake</c> has not assigned
        /// yet, and <c>SwitchToEmbed</c> clears <c>nest.macro</c> — so the machine's own <c>Awake</c> would
        /// then read that null as the embedded-graph case, drop the reference to the instance the switch had
        /// created, and leave it with nothing able to free it. Which of those happens would be decided by
        /// component order, silently.
        /// </para>
        ///
        /// <para>
        /// The agent is built inactive so no lifecycle method has run when <c>Switch</c> is called — the one
        /// state in which this is reproducible from a test.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator SwitchingBeforeAwakeIsRefusedRatherThanCorrupting()
        {
            var machine = SpawnDormant();

            LogAssert.Expect(LogType.Error, new Regex("before its own Awake"));

            machine.Switch(BuildGuardedTree(guardAnswers: true));

            Assert.IsTrue(machine.GraphInstance == null,
                "The call is ignored, so no instance is created -- one made here would be orphaned the moment "
                + "Awake ran and could never be destroyed.");

            yield return null;
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
        /// Entry -&gt; Wait(0), with no Repeater over it — so the root reaches <c>Success</c> on its first tick
        /// and the machine halts. The state a switch has to be able to bring an agent back out of.
        /// </summary>
        private BehaviorTreeGraphAsset BuildFinishingTree()
        {
            var tree = NewTree();
            var graph = tree.graph;

            var wait = Add<WaitTime>(graph, 0.0f, 100.0f);
            FeedFloat(graph, wait, wait.Time, 0.0f);

            Connect(graph, graph.EntryNode, wait);

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

        /// <summary>
        /// Entry -&gt; Repeater -&gt; Run Behavior Tree -&gt; (a branch that itself runs a branch that holds
        /// forever). Two levels, because one level cannot tell whether the release walks or just reaches.
        /// </summary>
        private BehaviorTreeGraphAsset BuildTreeWithNestedSubTrees()
        {
            var innermost = NewTree();
            var hold = Add<WaitTime>(innermost.graph, 0.0f, 100.0f);
            FeedFloat(innermost.graph, hold, hold.Time, 999.0f);
            Connect(innermost.graph, innermost.graph.EntryNode, hold);

            var middle = NewTree();
            var innerCall = Add<RunBehaviorTreeGraphNode>(middle.graph, 0.0f, 100.0f);
            innerCall.SetBehaviorTreeGraphAsset(innermost);
            Connect(middle.graph, middle.graph.EntryNode, innerCall);

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var outerCall = Add<RunBehaviorTreeGraphNode>(graph, 0.0f, 250.0f);
            outerCall.SetBehaviorTreeGraphAsset(middle);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, outerCall);

            return tree;
        }

        private static void Declare(GameObject _, Unity.VisualScripting.Variables variables)
        {
            variables.declarations.Set("alwaysTrue", true);
            variables.declarations.Set("alwaysFalse", false);
        }

        #endregion
    }
}

using System;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A node that did not enter must not be ticked as though it had.
    ///
    /// <para>
    /// Two routes led to the same place. A composite ticks a child on the same frame it declined to enter
    /// it — <c>Selector</c> calls <c>OnNodeEnter</c> and then <c>OnUpdateInternal</c> in one iteration — and
    /// <c>OnUpdateInternal</c> used to <em>re-ask</em> the guards rather than reuse the entry verdict. Since
    /// nothing promises a guard is stable within a frame, entry could answer false and the tick that
    /// followed it answer true, and then <c>OnUpdate</c> ran on a node whose <c>OnEnter</c> never did
    /// (finding 2.2). Separately, a node whose <c>OnEnter</c> <em>threw</em> was left flagged as running,
    /// and every caller uses that flag to decide between entering and ticking — so the next frame ticked it
    /// on state its <c>OnEnter</c> never finished setting (finding 2.11, fixed in GraphCore's
    /// <c>BaseGraphNode</c>).
    /// </para>
    ///
    /// <para>
    /// The damage in both cases is the same and it is not the exception: it is the branch afterwards
    /// reporting <see cref="ExecutionStatus.Success"/>. A <c>WaitTime</c> assigns its timer in
    /// <c>OnEnter</c>, so a tick that skipped entry counts down from zero and succeeds as though the wait
    /// had elapsed. <em>A tree that silently succeeds is far harder to diagnose than one that stops.</em>
    /// </para>
    /// </summary>
    [TestFixture]
    public class EntryVerdictTests
    {
        private static T AddNode<T>(BehaviorTreeGraph graph, float y = 100.0f) where T : BehaviorTreeNode, new()
        {
            var node = new T { Position = new Rect(0.0f, y, 150.0f, 100.0f) };
            graph.Nodes.Add(node);

            return node;
        }

        private static void Connect(BehaviorTreeGraph graph, BehaviorTreeNode parent, BehaviorTreeNode child)
        {
            var transition = new BehaviorTreeTransition();
            transition.SetupTransition(parent, child, graph.CountTransitionsFromNode(parent));
            graph.Transitions.Add(transition);
        }

        #region 2.2 -- the entry verdict is taken once per frame

        /// <summary>
        /// The finding itself. The guard says no at entry and yes when asked again; the node must still not
        /// run, because the question was already answered this frame.
        /// </summary>
        [Test]
        public void AGuardThatFlipsWithinTheFrameCannotUndoARefusedEntry()
        {
            var graph = new BehaviorTreeGraph();
            var node = AddNode<ScriptedNode>(graph);
            var guard = AddNode<UnstableGuard>(graph, 300.0f);

            Connect(graph, graph.EntryNode, node);
            guard.UpdateOwner(node);

            graph.OnAwake();

            node.OnNodeEnter();

            Assert.AreEqual(0, node.EnterCalls, "the guard refused, so nothing should have entered");

            var status = node.OnUpdateInternal();

            Assert.AreEqual(ExecutionStatus.Failure, status,
                "the branch was turned away this frame and stays turned away for it.");
            Assert.AreEqual(0, node.UpdateCalls,
                "OnUpdate must not run on a node whose OnEnter never did. This is the whole defect: the "
                + "guard answers true the second time it is asked, and the tick used to ask again.");
        }

        /// <summary>
        /// The other half of the same change, and the reason it is also cheaper: a declined entry asks its
        /// guards once, not twice. Every doorman on every refused branch was being evaluated a second time
        /// for an answer that was already known.
        /// </summary>
        [Test]
        public void ARefusedEntryEvaluatesItsGuardsOnce()
        {
            var graph = new BehaviorTreeGraph();
            var node = AddNode<ScriptedNode>(graph);
            var guard = AddNode<CountingGuard>(graph, 300.0f);

            Connect(graph, graph.EntryNode, node);
            guard.UpdateOwner(node);
            guard.Result = false;

            graph.OnAwake();

            node.OnNodeEnter();
            node.OnUpdateInternal();

            Assert.AreEqual(1, guard.Evaluations,
                "entry decided it; the tick in the same frame reuses that decision rather than paying for "
                + "it again.");
        }

        /// <summary>
        /// A refusal is a verdict about a frame, not a memory.
        ///
        /// <para>
        /// <c>Parallel</c> and <c>Entry</c> enter their children once and then tick them every frame
        /// afterwards, so a refusal that outlived its frame would mean a branch turned away once could never
        /// start again — a far worse bug than the one being fixed. What the engine currently does with a
        /// never-entered node on a <em>later</em> frame is a separate question this change deliberately does
        /// not touch, which is why the assertion is that the guards were asked again rather than about the
        /// status that came back.
        /// </para>
        /// </summary>
        [Test]
        public void ARefusalDoesNotOutliveItsFrame()
        {
            var graph = new BehaviorTreeGraph();
            var node = AddNode<ScriptedNode>(graph);
            var guard = AddNode<CountingGuard>(graph, 300.0f);

            Connect(graph, graph.EntryNode, node);
            guard.UpdateOwner(node);
            guard.Result = false;

            graph.OnAwake();

            node.OnNodeEnter();
            Assert.AreEqual(ExecutionStatus.Failure, node.OnUpdateInternal());

            int afterItsOwnFrame = guard.Evaluations;

            // A later frame, with no fresh entry attempt -- exactly what Parallel does to its children.
            AdvanceFrame(node);
            node.OnUpdateInternal();

            Assert.Greater(guard.Evaluations, afterItsOwnFrame,
                "once the refusal's frame has passed the guards are asked again, exactly as before this "
                + "change. Reusing a stale verdict would strand the branch permanently.");
        }

        /// <summary>
        /// A node that entered cleanly is unaffected: the running path never consults the verdict, and only
        /// guards that claim the right to interrupt get a say once a branch is under way.
        /// </summary>
        [Test]
        public void AnAllowedEntryStillTicksNormally()
        {
            var graph = new BehaviorTreeGraph();
            var node = AddNode<ScriptedNode>(graph);
            var guard = AddNode<CountingGuard>(graph, 300.0f);

            node.DefaultResult = ExecutionStatus.Running;

            Connect(graph, graph.EntryNode, node);
            guard.UpdateOwner(node);

            graph.OnAwake();

            node.OnNodeEnter();

            Assert.AreEqual(1, node.EnterCalls);
            Assert.AreEqual(ExecutionStatus.Running, node.OnUpdateInternal());
            Assert.AreEqual(1, node.UpdateCalls);
        }

        #endregion

        #region 2.11 -- a failed entry is not an entry

        /// <summary>
        /// <c>OnEnter</c> threw, so the node never entered — and <c>IsRunning</c> has to say so, because
        /// that flag is the only thing callers consult when choosing between entering a node and ticking it
        /// (<c>Cooldown</c>: <c>if (!task.IsRunning) task.OnNodeEnter();</c>).
        /// </summary>
        [Test]
        public void ANodeWhoseEnterThrewIsNotLeftMarkedRunning()
        {
            var node = new ThrowsOnEnterNode();

            Assert.Throws<InvalidOperationException>(() => node.OnNodeEnter(),
                "the exception still propagates — this is about what it leaves behind, not about swallowing it");

            Assert.IsFalse(node.IsRunning,
                "a node that failed to enter has not entered. Left flagged running, its next tick runs "
                + "OnUpdate on state OnEnter never finished setting.");
        }

        /// <summary>
        /// The consequence that actually costs a project a debugging afternoon, stated end to end: the
        /// caller's own enter-or-tick decision must send a failed node back to entry rather than into
        /// <c>OnUpdate</c>. Ticking it would have reported Success.
        /// </summary>
        [Test]
        public void AFailedEntryIsRetriedRatherThanTickedIntoAFalseSuccess()
        {
            var node = new ThrowsOnEnterNode();

            Assert.Throws<InvalidOperationException>(() => node.OnNodeEnter());

            // The decision every composite makes, written out: enter what is not running, tick what is.
            if (!node.IsRunning)
            {
                Assert.Throws<InvalidOperationException>(() => node.OnNodeEnter(),
                    "it is entered again, and fails again — visibly, every frame, rather than once.");
            }
            else
            {
                node.OnUpdateInternal();
            }

            Assert.AreEqual(0, node.UpdateCalls,
                "OnUpdate reports Success for this node. Reaching it after a failed entry is how a branch "
                + "silently claims to have finished work it never started.");
        }

        /// <summary>
        /// A failed entry leaves the node faulted rather than merely un-run, so the canvas paints it —
        /// <c>CanvasUpdate</c> reads <see cref="ExecutionStatus.Exception"/> and nothing else. Only
        /// <c>OnUpdateInternal</c>'s catch used to set it, so a node that failed to <em>enter</em> looked
        /// perfectly healthy while one that failed to <em>update</em> went red.
        /// </summary>
        [Test]
        public void AFailedEntryReportsTheExceptionStatus()
        {
            var node = new ThrowsOnEnterNode();

            Assert.Throws<InvalidOperationException>(() => node.OnNodeEnter());

            Assert.AreEqual(ExecutionStatus.Exception, node.LastExecutionStatus);
        }

        /// <summary>
        /// And it is not exited either. <c>OnNodeExit</c> already returns early for a node that is not
        /// running, so this follows from the fix rather than being separate — but it is the deliberate half
        /// of the trade and worth stating: running <c>OnExit</c> against half-initialised state is the same
        /// hazard wearing a different hat, so a node that never entered is left entirely alone.
        /// </summary>
        [Test]
        public void AFailedEntryIsNotExitedEither()
        {
            var node = new ThrowsOnEnterNode();

            Assert.Throws<InvalidOperationException>(() => node.OnNodeEnter());

            node.OnNodeExit();

            Assert.AreEqual(0, node.ExitCalls,
                "OnExit would be tearing down an entry that never completed.");
        }

        #endregion

        #region The composites, which decide this for themselves

        /// <summary>
        /// A <c>Selector</c> whose child throws on entry retries the entry rather than ticking it.
        ///
        /// <para>
        /// The node-level fix is not enough on its own here, and this is the case that proves it.
        /// <c>Selector</c> does not consult <c>IsRunning</c> — it keeps its own <c>callOnEnter</c> flag, and
        /// cleared it <em>before</em> calling <c>OnNodeEnter</c>. So a throwing entry left the selector
        /// believing it had entered, and the next frame went straight to <c>OnUpdateInternal</c> on a node
        /// whose <c>OnEnter</c> never ran: the same silent success, reached through a cached flag instead of
        /// through the running flag.
        /// </para>
        /// </summary>
        [Test]
        public void ASelectorRetriesAChildWhoseEntryThrewInsteadOfTickingIt()
        {
            var graph = new BehaviorTreeGraph();
            var selector = AddNode<Selector>(graph);
            var child = new ThrowsOnEnterNode { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, selector);
            Connect(graph, selector, child);

            graph.OnAwake();

            selector.OnNodeEnter();

            Assert.Throws<InvalidOperationException>(() => selector.OnUpdateInternal(),
                "the child's entry throws and the selector lets it out");

            // The next frame. Nothing has changed, so it must fail the same visible way.
            Assert.Throws<InvalidOperationException>(() => selector.OnUpdateInternal(),
                "the entry is retried rather than skipped -- a failure that repeats is one someone can find.");

            Assert.AreEqual(0, child.UpdateCalls,
                "OnUpdate reports Success for this child. Reaching it after a failed entry is how a branch "
                + "quietly claims to have done work it never started.");
        }

        /// <summary>The same for <c>Sequence</c>, which keeps the identical flag for the identical reason.</summary>
        [Test]
        public void ASequenceRetriesAChildWhoseEntryThrewInsteadOfTickingIt()
        {
            var graph = new BehaviorTreeGraph();
            var sequence = AddNode<Sequence>(graph);
            var child = new ThrowsOnEnterNode { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(child);

            Connect(graph, graph.EntryNode, sequence);
            Connect(graph, sequence, child);

            graph.OnAwake();

            sequence.OnNodeEnter();

            Assert.Throws<InvalidOperationException>(() => sequence.OnUpdateInternal());
            Assert.Throws<InvalidOperationException>(() => sequence.OnUpdateInternal());

            Assert.AreEqual(0, child.UpdateCalls);
        }

        /// <summary>
        /// A container that enters several children at once unwinds the ones that got in when a later one
        /// throws.
        ///
        /// <para>
        /// This is the cost of clearing the running flag on a failed entry, and it has to be paid rather
        /// than accepted. The container's own entry failed, so it is not running, so its
        /// <c>OnNodeExit</c> returns early and the sweep that would have exited its children never happens —
        /// leaving the first child marked running for the rest of the session, and a later re-entry calling
        /// <c>OnNodeEnter</c> on it a second time with no exit in between. That is the double-entry the
        /// composites warn about by name: a Wait resets its timer, an animation restarts.
        /// </para>
        /// </summary>
        [Test]
        public void AContainerUnwindsTheChildrenItAlreadyEnteredWhenALaterOneThrows()
        {
            var graph = new BehaviorTreeGraph();
            var parallel = AddNode<ParallelSequence>(graph);

            var first = new ScriptedNode(ExecutionStatus.Running) { Position = new Rect(0.0f, 300.0f, 150.0f, 100.0f) };
            var second = new ThrowsOnEnterNode { Position = new Rect(200.0f, 300.0f, 150.0f, 100.0f) };
            graph.Nodes.Add(first);
            graph.Nodes.Add(second);

            Connect(graph, graph.EntryNode, parallel);
            Connect(graph, parallel, first);
            Connect(graph, parallel, second);

            graph.OnAwake();

            Assert.Throws<InvalidOperationException>(() => parallel.OnNodeEnter());

            Assert.IsFalse(first.IsRunning,
                "the first child got in before the second failed, and nothing else will ever exit it: the "
                + "container is not running, so its own exit sweep does not run.");
            Assert.AreEqual(1, first.ExitCalls, "so the container has to exit it on the way out.");
        }

        #endregion

        /// <summary>
        /// Moves past the frame a verdict was stamped with. <c>Time.frameCount</c> does not advance inside a
        /// synchronous edit-mode test, so the stamp is aged directly — the alternative is a
        /// <c>[UnityTest]</c> coroutine, which would buy nothing but a slower suite for a field this test
        /// already knows the name of.
        /// </summary>
        private static void AdvanceFrame(BehaviorTreeNode node)
        {
            var field = typeof(BehaviorTreeNode).GetField(
                "entryRefusalFrame",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                "BehaviorTreeNode.entryRefusalFrame is what scopes an entry refusal to one frame; if it was "
                + "renamed, this test needs to follow it.");

            field.SetValue(node, Time.frameCount - 1);
        }
    }
}

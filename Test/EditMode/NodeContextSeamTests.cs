using System;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The context seam: a node body may be written against <see cref="BTContext"/> or against the
    /// parameterless hooks, and both run at exactly the moments they always did.
    ///
    /// <para>
    /// These are the tests that make the seam safe to land in a release. The whole claim of design spec 07
    /// step 1 is "behaviour unchanged, both node styles coexist", and the two halves of that are separable
    /// failures: a forwarding default that stops forwarding silently breaks every node written before the
    /// seam (including every node written by a user of BH3), and a dispatch that runs both the context body
    /// and the legacy body would double every side effect in a node that overrode both.
    /// </para>
    /// </summary>
    [TestFixture]
    public class NodeContextSeamTests
    {
        /// <summary>A node written the new way — every body takes a context, none touch the legacy hooks.</summary>
        private sealed class ContextNode : BehaviorTreeNode
        {
            public int AwakeCalls { get; private set; }
            public int EnterCalls { get; private set; }
            public int UpdateCalls { get; private set; }
            public int ExitCalls { get; private set; }

            /// <summary>Proves the context arrived rather than merely that the overload was chosen.</summary>
            public bool SawAContext { get; private set; }

            public ExecutionStatus Result { get; set; } = ExecutionStatus.Success;

            public override string NodeName => "Context Test Node";

            public override void OnAwake(BTContext ctx) => AwakeCalls++;

            public override void OnEnter(BTContext ctx)
            {
                EnterCalls++;
                // Machine is null here (no live machine in edit mode), so the context is confirmed by
                // something it can answer without one.
                SawAContext = ctx.GetType() == typeof(BTContext);
            }

            public override ExecutionStatus OnUpdate(BTContext ctx)
            {
                UpdateCalls++;
                return Result;
            }

            public override void OnExit(BTContext ctx) => ExitCalls++;
        }

        /// <summary>A node written the old way. Stands in for every node that predates the seam.</summary>
        private sealed class LegacyNode : BehaviorTreeNode
        {
            public int AwakeCalls { get; private set; }
            public int EnterCalls { get; private set; }
            public int UpdateCalls { get; private set; }
            public int ExitCalls { get; private set; }

            public override string NodeName => "Legacy Test Node";

            public override void OnAwake() => AwakeCalls++;
            public override void OnEnter() => EnterCalls++;
            public override void OnExit() => ExitCalls++;

            public override ExecutionStatus OnUpdate()
            {
                UpdateCalls++;
                return ExecutionStatus.Success;
            }
        }

        /// <summary>Half migrated: the tick takes a context, the entry has not been moved yet.</summary>
        private sealed class HalfMigratedNode : BehaviorTreeNode
        {
            public int LegacyEnterCalls { get; private set; }
            public int ContextUpdateCalls { get; private set; }

            public override string NodeName => "Half Migrated Test Node";

            public override void OnEnter() => LegacyEnterCalls++;

            public override ExecutionStatus OnUpdate(BTContext ctx)
            {
                ContextUpdateCalls++;
                return ExecutionStatus.Success;
            }
        }

        /// <summary>Reads its only port through the context rather than off the port object.</summary>
        private sealed class PortReadingContextNode : BehaviorTreeNode
        {
            public const float DefaultAmount = 7.5f;

            [DoNotSerialize] public ValueInput Amount { get; private set; }

            public float ReadValue { get; private set; }

            public override string NodeName => "Port Reading Context Test Node";

            protected override void Definition()
            {
                base.Definition();
                Amount = ValueInput<float>(nameof(Amount), DefaultAmount);
            }

            public override void OnEnter(BTContext ctx) => ReadValue = ctx.GetValue<float>(Amount);
        }

        /// <summary>
        /// A node overriding both styles. Nothing should write one — it exists to prove the dispatch picks
        /// one body rather than running both, which is the failure that would double every side effect.
        /// </summary>
        private sealed class BothStylesNode : BehaviorTreeNode
        {
            public int LegacyEnterCalls { get; private set; }
            public int ContextEnterCalls { get; private set; }

            public override string NodeName => "Both Styles Test Node";

            public override void OnEnter() => LegacyEnterCalls++;

            public override void OnEnter(BTContext ctx) => ContextEnterCalls++;
        }

        /// <summary>A node written the way the docs now teach: state in a memory class, not in fields.</summary>
        private sealed class MemoryUsingNode : BehaviorTreeNode
        {
            // private, exactly as custom-nodes.md teaches it -- so this fixture also proves the documented
            // pattern compiles against the generic constraint on ctx.Memory<T>().
            private sealed class Memory
            {
                public float Elapsed;
            }

            public override string NodeName => "Memory Using Test Node";

            public override void OnEnter(BTContext ctx) => ctx.Memory<Memory>().Elapsed = 0f;

            public override ExecutionStatus OnUpdate(BTContext ctx)
            {
                var memory = ctx.Memory<Memory>();
                memory.Elapsed += 1f;
                return memory.Elapsed >= 3f ? ExecutionStatus.Success : ExecutionStatus.Running;
            }

            /// <summary>Reads its own memory from outside a hook. Context is protected, so the node asks.</summary>
            public float Elapsed => Context.Memory<Memory>().Elapsed;
        }

        /// <summary>Asks for two different memory types, which is an authoring mistake rather than a feature.</summary>
        private sealed class TwoMemoryTypesNode : BehaviorTreeNode
        {
            private sealed class First { }
            private sealed class Second { }

            public override string NodeName => "Two Memory Types Test Node";

            public override void OnEnter(BTContext ctx)
            {
                ctx.Memory<First>();
                ctx.Memory<Second>();
            }
        }

        [Test]
        public void Memory_PersistsAcrossHooksOnTheSameNode()
        {
            // The whole substitute for an instance field: state set in OnEnter has to survive into OnUpdate,
            // or nothing can be migrated off fields.
            var node = new MemoryUsingNode();

            var status = node.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Success, status,
                "The node counts to three across ticks, which only works if its memory survives between them.");
        }

        [Test]
        public void Memory_IsNotSharedBetweenTwoNodesOfTheSameType()
        {
            // Per-agent means per-node-instance today, because the tree is cloned per agent. If two
            // instances shared a block, migrating a node off fields would introduce exactly the bug the
            // shared-tree refactor is trying to avoid.
            var first = new MemoryUsingNode();
            var second = new MemoryUsingNode();

            first.OnNodeEnter();
            first.OnUpdateInternal();
            first.OnUpdateInternal();

            second.OnNodeEnter();

            Assert.AreEqual(2f, first.Elapsed, "The first node's memory should hold its own two ticks.");
            Assert.AreEqual(0f, second.Elapsed, "The second node must not see the first node's state.");
        }

        [Test]
        public void Memory_RefusesASecondTypeOnTheSameNode()
        {
            var node = new TwoMemoryTypesNode();

            var error = Assert.Throws<InvalidOperationException>(() => node.OnNodeEnter());

            Assert.That(error.Message, Does.Contain("one memory type"),
                "The failure should tell the author to use a single memory class, not just that a cast failed.");
        }

        [Test]
        public void ContextOverloads_RunForEnterUpdateAndExit()
        {
            var node = new ContextNode();

            node.RunToCompletion();

            Assert.AreEqual(1, node.EnterCalls, "OnEnter(BTContext) should have run once.");
            Assert.AreEqual(1, node.UpdateCalls, "OnUpdate(BTContext) should have run once.");
            Assert.AreEqual(1, node.ExitCalls, "OnExit(BTContext) should have run once.");
            Assert.IsTrue(node.SawAContext, "The node's body should have been handed a BTContext.");
        }

        [Test]
        public void AwakeNode_RunsTheContextOverload()
        {
            var node = new ContextNode();

            node.AwakeNode();

            Assert.AreEqual(1, node.AwakeCalls,
                "The graph's awake pass calls AwakeNode, which must reach OnAwake(BTContext).");
        }

        [Test]
        public void LegacyNode_StillRunsEveryHook()
        {
            // The regression that matters most: this is every node written before the seam existed, and
            // every node written by a user of BH3 against the documented API of the previous release.
            var node = new LegacyNode();

            node.AwakeNode();
            node.RunToCompletion();

            Assert.AreEqual(1, node.AwakeCalls, "The parameterless OnAwake must still run.");
            Assert.AreEqual(1, node.EnterCalls, "The parameterless OnEnter must still run.");
            Assert.AreEqual(1, node.UpdateCalls, "The parameterless OnUpdate must still run.");
            Assert.AreEqual(1, node.ExitCalls, "The parameterless OnExit must still run.");
        }

        [Test]
        public void LegacyNode_ReportsItsStatusThroughTheNormalTickPath()
        {
            var node = new LegacyNode();

            var status = node.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Success, status,
                "Forwarding must not swallow the status the legacy OnUpdate returned.");
        }

        [Test]
        public void ContextNode_ReportsItsStatusThroughTheNormalTickPath()
        {
            var node = new ContextNode { Result = ExecutionStatus.Failure };

            var status = node.RunToCompletion();

            Assert.AreEqual(ExecutionStatus.Failure, status,
                "OnUpdate(BTContext)'s return value must reach the caller unchanged.");
        }

        [Test]
        public void HalfMigratedNode_RunsBothStylesAtTheRightMoments()
        {
            // Migration is per-method, not per-node — a node part-way through must work, or nobody can
            // migrate anything incrementally.
            var node = new HalfMigratedNode();

            node.RunToCompletion();

            Assert.AreEqual(1, node.LegacyEnterCalls, "The un-migrated OnEnter should still run.");
            Assert.AreEqual(1, node.ContextUpdateCalls, "The migrated OnUpdate(BTContext) should still run.");
        }

        [Test]
        public void OverridingTheContextOverload_DoesNotAlsoRunTheLegacyBody()
        {
            var node = new BothStylesNode();

            node.RunToCompletion();

            Assert.AreEqual(1, node.ContextEnterCalls, "The context overload should have run.");
            Assert.AreEqual(0, node.LegacyEnterCalls,
                "Dispatch must pick one body. Running both would double every side effect in a node that "
                + "overrode the context overload without removing its legacy one.");
        }

        [Test]
        public void Context_ReadsAPortValue()
        {
            var node = new PortReadingContextNode();
            node.Define();

            node.RunToCompletion();

            Assert.AreEqual(PortReadingContextNode.DefaultAmount, node.ReadValue,
                "ctx.GetValue<T>(port) must read the port exactly as port.GetValue<T>() does.");
        }

        [Test]
        public void ARefusedEntry_NeverReachesTheContextOverload()
        {
            // The seam sits inside the existing guard filter rather than beside it. If it did not, a node
            // whose guard turned it away would still have its body run — the exact hazard the sealed
            // OnNodeEnter exists to prevent.
            var node = new ContextNode();
            var guard = new CountingGuard { Result = false };
            node.AddConditionalExecution(guard);

            node.OnNodeEnter();

            Assert.AreEqual(0, node.EnterCalls,
                "A guard that refused entry must stop the context body running, exactly as it stops the "
                + "legacy body.");
        }

        [Test]
        public void APassingGuard_StillLetsTheContextOverloadRun()
        {
            var node = new ContextNode();
            var guard = new CountingGuard { Result = true };
            node.AddConditionalExecution(guard);

            node.OnNodeEnter();

            Assert.AreEqual(1, node.EnterCalls, "A guard that passed must not block the context body.");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ArcaneOnyx.BehaviorTree.Debugging;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A variable node cannot start life pointed at a store the tree has no way to reach.
    ///
    /// <para>
    /// It used to. The field was a <see cref="Unity.VisualScripting.VariableKind"/>, whose zero value is
    /// <c>Flow</c> — per-invocation scratch that exists only inside a running
    /// <c>Unity.VisualScripting.Flow</c>, and that a behavior tree has nothing to offer. So every freshly
    /// dropped Get/Set/Remove Variable node defaulted to the one option that could not work, and said so
    /// only at runtime: a silent null out of a read, a console error out of a write, one frame at a time and
    /// nowhere near the node that caused it. One such node is still sitting in a shipped demo asset.
    /// </para>
    ///
    /// <para>
    /// These cover the two halves of the fix. The enum half: the type owns its own members, none of them is
    /// Flow, and the names still match Unity's so that no asset on disk notices the change. The node half:
    /// an unconfigured node reports itself on the canvas and refuses to guess if it runs anyway — the same
    /// bargain <c>VariableKeyPort</c> already strikes for a forgotten key.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeVariableKindTests
    {
        /// <summary>Every node whose store field actually decides where it goes.</summary>
        private static readonly Type[] StoreHonouringNodes =
        {
            typeof(GetVariable), typeof(SetVariable), typeof(RemoveVariable),
            typeof(GenerateRandomNavMeshPosition)
        };

        private static FieldInfo KindField(Type nodeType) =>
            nodeType.GetField("VariableKind", BindingFlags.NonPublic | BindingFlags.Instance);

        private static BehaviorTreeNode Node(Type nodeType, BehaviorTreeVariableKind kind, string key = "hp")
        {
            var node = (BehaviorTreeNode)Activator.CreateInstance(nodeType);
            node.Define();
            KindField(nodeType).SetValue(node, kind);

            var keyProperty = nodeType.GetProperty("Key") ?? nodeType.GetProperty("PositionKey");
            ((ValueInput)keyProperty?.GetValue(node))?.SetDefaultValue(key);

            return node;
        }

        private static T Node<T>(BehaviorTreeVariableKind kind, string key = "hp") where T : BehaviorTreeNode =>
            (T)Node(typeof(T), kind, key);

        private static List<NodeProblem> ProblemsOf(BehaviorTreeNode node)
        {
            var problems = new List<NodeProblem>();
            node.CollectProblems(problems);
            return problems;
        }

        // ------------------------------------------------------------------ the enum itself

        [Test]
        public void TheEnum_HasNoFlowMember()
        {
            CollectionAssert.DoesNotContain(Enum.GetNames(typeof(BehaviorTreeVariableKind)), "Flow",
                "Flow is per-invocation scratch on a Unity flow. A behavior tree node outlives any flow, so "
                + "offering the option can only ever produce a node that does nothing.");
        }

        [Test]
        public void None_IsTheZeroValue()
        {
            // Not cosmetic: this is the value a node arrives at with no author input, and the reason the
            // canvas can tell "nobody chose" apart from "somebody chose agent state".
            Assert.AreEqual(BehaviorTreeVariableKind.None, default(BehaviorTreeVariableKind));
        }

        [Test]
        public void EveryStore_KeepsUnityName_SoAssetsOnDiskStillLoad()
        {
            // Visual Scripting serializes an enum by name (fsEnumConverter, SerializeEnumsAsInteger = false),
            // so name equality -- not ordering, not the numeric value -- is the whole compatibility contract
            // with the kinds already written into tree assets in this repo.
            var unityNames = Enum.GetNames(typeof(Unity.VisualScripting.VariableKind));

            var missing = Enum.GetNames(typeof(BehaviorTreeVariableKind))
                .Where(name => name != nameof(BehaviorTreeVariableKind.None))
                .Where(name => !unityNames.Contains(name))
                .ToArray();

            CollectionAssert.IsEmpty(missing,
                "Renaming a store breaks every asset that already stored it -- the value fails to "
                + "deserialize rather than falling back:\n" + string.Join("\n", missing));
        }

        [Test]
        public void None_IsRenamedFromFlow_SoANodeOnFlowMigratesRatherThanFailingToLoad()
        {
            var renamed = typeof(BehaviorTreeVariableKind)
                .GetField(nameof(BehaviorTreeVariableKind.None))
                .GetCustomAttributes<RenamedFromAttribute>()
                .Select(attribute => attribute.previousName)
                .ToArray();

            CollectionAssert.Contains(renamed, "Flow",
                "Without this the deserializer rejects the value outright (\"Cannot find enum name Flow\"), "
                + "which turns a misconfigured node into an asset that will not load at all.");
        }

        // ------------------------------------------------------------------ assets on disk

        [Test]
        public void AStoredKind_RoundTripsThroughSerialization()
        {
            foreach (var kind in Enum.GetValues(typeof(BehaviorTreeVariableKind)).Cast<BehaviorTreeVariableKind>())
            {
                var data = Serialization.Serialize((object)Node<GetVariable>(kind));

                StringAssert.Contains("\"VariableKind\":\"" + kind + "\"", data.json,
                    "Stored by name, not by ordinal -- were it ever a number, reordering the enum would "
                    + "silently repoint every node in the project.");

                Assert.AreEqual(kind, KindField(typeof(GetVariable)).GetValue(Serialization.Deserialize(data)),
                    kind + " did not survive a round trip.");
            }
        }

        [Test]
        public void ANodeStoredAsFlow_LoadsAsNone()
        {
            // Exactly the shape of the GetVariable sitting in FPSDemo/Assets/Move.asset today.
            var authored = Serialization.Serialize((object)Node<GetVariable>(BehaviorTreeVariableKind.Graph));
            var onDisk = new SerializationData(
                authored.json.Replace("\"VariableKind\":\"Graph\"", "\"VariableKind\":\"Flow\""),
                authored.objectReferences);

            Assert.AreEqual(BehaviorTreeVariableKind.None,
                KindField(typeof(GetVariable)).GetValue(Serialization.Deserialize(onDisk)),
                "A node that was on Flow was already broken. It should arrive as visibly unconfigured, not "
                + "quietly repointed at a store its author never chose.");
        }

        // ------------------------------------------------------------------ the nodes

        [Test]
        public void EveryStorePickingNode_ReportsItselfWhenNoStoreIsChosen()
        {
            var silent = StoreHonouringNodes
                .Where(type => !ProblemsOf(Node(type, BehaviorTreeVariableKind.None))
                    .Any(problem => problem.Summary.Contains("variable store")))
                .Select(type => type.Name)
                .ToArray();

            CollectionAssert.IsEmpty(silent,
                "These nodes route on a store and stay quiet when none is chosen, so the mistake survives "
                + "until something runs them:\n" + string.Join("\n", silent));
        }

        /// <summary>
        /// The Key rule reaches the same four nodes as the store rule.
        /// </summary>
        /// <remarks>
        /// <c>GenerateRandomNavMeshPosition</c> was the odd one out: it read <c>PositionKey</c> raw instead
        /// of through <see cref="VariableKeyPort"/>, so an empty key wrote a variable named <c>""</c> and
        /// still reported Success — a fact published under a name no guard will ever watch, with nothing on
        /// the canvas to say so. Checked across the set rather than on that one node, because the way this
        /// gap appears is a node being written without the shared rule wired in.
        /// </remarks>
        [Test]
        public void EveryStoreHonouringNode_ReportsItselfWhenNoKeyIsChosen()
        {
            var silent = StoreHonouringNodes
                .Where(type => !ProblemsOf(Node(type, BehaviorTreeVariableKind.Object, key: string.Empty))
                    .Any(problem => problem.Summary.Contains("variable key")))
                .Select(type => type.Name)
                .ToArray();

            CollectionAssert.IsEmpty(silent,
                "These nodes act on a variable by name and stay quiet when the name is blank:\n"
                + string.Join("\n", silent));
        }

        /// <summary>
        /// The runtime half of the same rule, on the node that was missing it.
        /// </summary>
        /// <remarks>
        /// Reaches the throw without a baked navmesh precisely because the key resolves before any sampling:
        /// <c>NavMesh.SamplePosition</c> would find nothing here, the loop would fall through to Failure, and
        /// a key resolved at the write site would never be reached at all — which is what let the raw read
        /// survive unnoticed.
        /// </remarks>
        [Test]
        public void GeneratingAPositionWithNoKey_ThrowsNamingTheNode()
        {
            var agent = new UnityEngine.GameObject("Zombie");

            try
            {
                var machine = agent.AddComponent<BindableMachine>();
                machine.BindVariables();

                var node = Node<GenerateRandomNavMeshPosition>(
                    BehaviorTreeVariableKind.Object, key: string.Empty);
                node.SetMachine(machine);

                var thrown = Assert.Throws<InvalidOperationException>(() => node.OnUpdate());

                StringAssert.Contains(node.NodeName, thrown.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(agent);
            }
        }

        [Test]
        public void AConfiguredNode_ReportsNoStoreProblem()
        {
            Assert.That(
                ProblemsOf(Node<GetVariable>(BehaviorTreeVariableKind.Object)).Select(problem => problem.Summary),
                Has.None.Contains("variable store"),
                "A stale badge is worse than no badge, because people trust it.");
        }

        [Test]
        public void TheStoreProblem_IsAnError_AndSaysWhatToDo()
        {
            var problem = ProblemsOf(Node<GetVariable>(BehaviorTreeVariableKind.None))
                .First(candidate => candidate.Summary.Contains("variable store"));

            Assert.AreEqual(NodeProblemSeverity.Error, problem.Severity,
                "The node cannot do anything at all, which is what Error means here.");
            Assert.IsNotNull(problem.Fix,
                "A diagnosis with no instruction is the half nobody can act on.");
        }

        [Test]
        public void ReadingFromNoStore_ThrowsNamingTheNode()
        {
            var node = Node<GetVariable>(BehaviorTreeVariableKind.None);

            var thrown = Assert.Throws<InvalidOperationException>(() => node.GetValue("hp", null),
                "Returning null was the old behaviour and the reason this stayed invisible: the reader "
                + "cannot tell an unset variable from a node that never had a store.");

            StringAssert.Contains(node.NodeName, thrown.Message,
                "A throw that does not name the node sends someone hunting through the whole tree.");
        }

        [Test]
        public void WritingToNoStore_ThrowsNamingTheNode()
        {
            var node = Node<SetVariable>(BehaviorTreeVariableKind.None);
            node.Value.SetDefaultValue(1.0f);

            var thrown = Assert.Throws<InvalidOperationException>(() => node.OnUpdate());

            StringAssert.Contains(node.NodeName, thrown.Message);
        }

        /// <summary>
        /// The guard sits above the recorder, not beside it.
        ///
        /// <para>
        /// <c>SaveVariable</c> records the write before performing it, so that "what did it change from" is
        /// still answerable. Rejecting an unchosen store after that point would leave a
        /// <c>VariableWrite</c> event describing a value nothing ever held — a row in the variable watch for
        /// a write that threw. The watch groups strictly on recorded events and has no idea the write
        /// failed, so the tool this change exists to make trustworthy would be the one telling the lie.
        /// </para>
        /// </summary>
        [Test]
        public void AWriteWithNoStore_IsRejectedBeforeItIsRecorded()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            var agent = new UnityEngine.GameObject("Zombie");

            try
            {
                // RequireComponent(typeof(Variables)) adds the store; BindVariables does what Awake would.
                var machine = agent.AddComponent<BindableMachine>();
                machine.BindVariables();
                machine.SetFlightRecorder(recorder);

                var node = new WritingNode();
                node.SetMachine(machine);
                node.SetFlightRecorder(recorder);

                // Proves the recorder is live before anything is asserted about it staying empty --
                // otherwise "no event" would pass for a recorder that could never record one.
                node.Write("alertLevel", BehaviorTreeVariableKind.Object, 2);
                Assert.AreEqual(1, recorder.EventCount, "precondition: this recorder records real writes");

                Assert.Throws<InvalidOperationException>(
                    () => node.Write("alertLevel", BehaviorTreeVariableKind.None, 3));

                Assert.AreEqual(1, recorder.EventCount,
                    "A write that never happened must not appear in the recording.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(agent);
                BehaviorTreeFlightRecorders.Reset();
            }
        }

        /// <summary>A <see cref="GameplayNode"/> exposing its protected write so a test can make one.</summary>
        private sealed class WritingNode : GameplayNode
        {
            public override string NodeName => "Writing Test Node";

            public void Write(string key, BehaviorTreeVariableKind kind, object value) =>
                SaveVariable(key, kind, value);
        }

        /// <summary>
        /// A machine whose <see cref="BehaviorTreeMachine.Variables"/> can be bound outside play mode, where
        /// <c>Awake</c> does not run.
        /// </summary>
        private sealed class BindableMachine : BehaviorTreeMachine
        {
            public void BindVariables() => Variables = GetComponent<Variables>();
        }

        /// <summary>
        /// The dropdown decides which store loses the key, which it did not used to.
        /// </summary>
        /// <remarks>
        /// <c>RemoveVariable</c> resolved its collection once from the agent's own declarations and removed
        /// from it whatever the author had picked, so choosing Application deleted an agent variable and
        /// reported Success — the node did something, just not the something on the node.
        /// </remarks>
        [Test]
        public void RemovingFromOneStore_LeavesTheOthersAlone()
        {
            var agent = new UnityEngine.GameObject("Zombie");

            try
            {
                var machine = agent.AddComponent<BindableMachine>();
                machine.BindVariables();

                // The same name in two stores, so a removal from the wrong one is visible rather than
                // indistinguishable from a removal from the right one.
                machine.Variables.declarations.Set("doomed", "agent");
                Variables.Application.Set("doomed", "application");

                var node = Node<RemoveVariable>(BehaviorTreeVariableKind.Application, "doomed");
                node.SetMachine(machine);

                Assert.AreEqual(GraphCore.ExecutionStatus.Success, node.OnUpdate());

                Assert.IsFalse(Variables.Application.IsDefined("doomed"),
                    "the chosen store is the one that loses the key");
                Assert.IsTrue(machine.Variables.declarations.IsDefined("doomed"),
                    "the agent store is not the chosen one and must be untouched");
            }
            finally
            {
                // No Application cleanup: removing "doomed" is what the node under test does, and Clear()
                // would take the other fixtures' keys with it.
                UnityEngine.Object.DestroyImmediate(agent);
            }
        }

        /// <summary>
        /// Removing an agent variable moves the version reactive guards compare against.
        /// </summary>
        /// <remarks>
        /// The node used to mutate the declarations directly. A guard watching <c>hasTarget</c> caches the
        /// version it last saw and only re-reads when it moves, so a key that vanished without a bump left
        /// the guard holding "true" forever — the branch kept running against a fact that no longer
        /// existed. Silent, and invisible to the watch and to breakpoints too, since nothing was recorded.
        /// </remarks>
        [Test]
        public void RemovingAnAgentVariable_MovesTheVersionAndIsRecorded()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            var agent = new UnityEngine.GameObject("Zombie");

            try
            {
                var machine = agent.AddComponent<BindableMachine>();
                machine.BindVariables();
                machine.SetFlightRecorder(recorder);

                var writer = AgentVariableWriter.On(agent);
                writer.SetAgentVariable("hasTarget", true);

                int before = writer.VersionOf("hasTarget");

                var node = Node<RemoveVariable>(BehaviorTreeVariableKind.Object, "hasTarget");
                node.SetMachine(machine);
                node.SetFlightRecorder(recorder);

                Assert.AreEqual(GraphCore.ExecutionStatus.Success, node.OnUpdate());

                Assert.IsFalse(machine.Variables.declarations.IsDefined("hasTarget"),
                    "precondition: the key is actually gone");
                Assert.Greater(writer.VersionOf("hasTarget"), before,
                    "A guard only re-reads when the version moves, so a removal that does not bump it "
                    + "leaves the guard holding a value that no longer exists.");
                Assert.AreEqual(1, recorder.EventCount,
                    "A removal is a change to null, and the watch and breakpoints need to see it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(agent);
                BehaviorTreeFlightRecorders.Reset();
            }
        }

        /// <summary>
        /// Removing a key that was never there is the state the node was asked for, so it is not an error
        /// and there is nothing to record.
        /// </summary>
        [Test]
        public void RemovingAKeyThatIsNotThere_SucceedsAndRecordsNothing()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");
            var agent = new UnityEngine.GameObject("Zombie");

            try
            {
                var machine = agent.AddComponent<BindableMachine>();
                machine.BindVariables();
                machine.SetFlightRecorder(recorder);

                var node = Node<RemoveVariable>(BehaviorTreeVariableKind.Object, "neverSet");
                node.SetMachine(machine);
                node.SetFlightRecorder(recorder);

                Assert.AreEqual(GraphCore.ExecutionStatus.Success, node.OnUpdate());
                Assert.AreEqual(0, recorder.EventCount, "nothing changed, so nothing to say");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(agent);
                BehaviorTreeFlightRecorders.Reset();
            }
        }

        /// <summary>
        /// Having nowhere to remove from is a different answer from having nothing to remove.
        /// </summary>
        /// <remarks>
        /// A Graph removal on a graph running without a scope is the only reachable case. Failure is a real
        /// answer a designer can see on the canvas; Success would be the quiet no-op this node spent years
        /// being.
        /// </remarks>
        [Test]
        public void RemovingFromAStoreThatIsNotThere_Fails()
        {
            var node = Node<RemoveVariable>(BehaviorTreeVariableKind.Graph, "scratch");

            Assert.AreEqual(GraphCore.ExecutionStatus.Failure, node.OnUpdate());
        }

        /// <summary>
        /// A branch removes its own copy of a shadowed name and leaves its caller's alone.
        /// </summary>
        /// <remarks>
        /// Scope isolation is what makes a sub-tree safe to reuse: two agents running the same branch must
        /// not be able to reach each other's scratch, and a branch must not be able to reach its caller's.
        /// A removal that walked the chain the way a <em>read</em> does would break that in the most
        /// destructive direction available — deleting a name out from under the caller rather than merely
        /// reading it.
        /// </remarks>
        [Test]
        public void RemovingAGraphVariable_TakesTheBranchsOwnCopyOnly()
        {
            var caller = new BehaviorTreeVariableScope(new VariableDeclarations());
            caller.Set("attempts", "callers");

            var branch = new BehaviorTreeVariableScope(new VariableDeclarations(), caller);
            branch.Set("attempts", "branchs");

            var node = Node<RemoveVariable>(BehaviorTreeVariableKind.Graph, "attempts");
            node.SetVariableScope(branch);

            Assert.AreEqual(GraphCore.ExecutionStatus.Success, node.OnUpdate());

            Assert.IsFalse(branch.Local.IsDefined("attempts"), "the branch's own copy is the one removed");
            Assert.AreEqual("callers", caller.Local.Get("attempts"),
                "a branch cannot delete a name out from under its caller");
        }

        /// <summary>
        /// A name only the caller defines is not this branch's to remove, so nothing happens and nothing is
        /// recorded.
        /// </summary>
        /// <remarks>
        /// The same isolation from the other side, and the case that would look like success if the removal
        /// walked outward: the node would report Success having deleted the caller's variable, and the
        /// recording would carry a write the branch had no business making.
        /// </remarks>
        [Test]
        public void RemovingAGraphVariableOnlyTheCallerDefines_ChangesNothing()
        {
            var recorder = new BehaviorTreeFlightRecorder("Zombie", "ZombieTree");

            try
            {
                var caller = new BehaviorTreeVariableScope(new VariableDeclarations());
                caller.Set("attempts", "callers");

                var branch = new BehaviorTreeVariableScope(new VariableDeclarations(), caller);

                var node = Node<RemoveVariable>(BehaviorTreeVariableKind.Graph, "attempts");
                node.SetVariableScope(branch);
                node.SetFlightRecorder(recorder);

                Assert.AreEqual(GraphCore.ExecutionStatus.Success, node.OnUpdate(),
                    "there was a store to remove from; the name simply was not in it");

                Assert.AreEqual("callers", caller.Local.Get("attempts"),
                    "the caller's variable is not this branch's to delete");
                Assert.AreEqual(0, recorder.EventCount,
                    "nothing changed, so the recording must not claim something did");
            }
            finally
            {
                BehaviorTreeFlightRecorders.Reset();
            }
        }

        [Test]
        public void AConfiguredRead_ReachesItsStore()
        {
            // Application variables need no scene, machine or agent, so this proves the guard lets a real
            // read through rather than throwing for everything.
            Variables.Application.Set("btVariableKindProbe", 42);

            Assert.AreEqual(42,
                Node<GetVariable>(BehaviorTreeVariableKind.Application).GetValue("btVariableKindProbe", null));
        }
    }
}

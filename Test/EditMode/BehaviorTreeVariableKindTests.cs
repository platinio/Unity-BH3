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
        /// <summary>
        /// Every node whose store field actually decides where it goes.
        /// </summary>
        /// <remarks>
        /// <c>RemoveVariable</c> is missing on purpose. It serializes the same field and never reads it —
        /// <c>OnUpdate</c> always removes from the agent's own declarations, whatever the dropdown says — so
        /// the canvas rule the others share would be telling an author to make a choice that has no effect.
        /// A pre-existing defect, carried through this change rather than fixed by it; when it is fixed,
        /// add the type here and this rule starts covering it.
        /// </remarks>
        private static readonly Type[] StoreHonouringNodes =
        {
            typeof(GetVariable), typeof(SetVariable), typeof(GenerateRandomNavMeshPosition)
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

using System.Collections;
using System.Linq;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BehaviorTree.Tests.PlayMode
{
    /// <summary>
    /// A tree built out of other trees — the thing BH3 exists for, running on a real machine.
    ///
    /// <para>
    /// The claim being tested is the one the whole modularity argument rests on: <b>a branch is a function
    /// that takes parameters and knows nothing about its caller.</b> A sub-tree declares what it needs, the
    /// <c>RunBehaviorTreeGraphNode</c> calling it grows a port per declaration, and the caller passes an
    /// argument at the call site. The branch never learns a variable name, which is what makes the same Patrol
    /// work on a Zombie, a Soldier and a Draugr.
    /// </para>
    ///
    /// <para>
    /// The edit-mode suite covers the contract statically — that declarations become ports, that an optional
    /// one carries its default and a required one throws unconnected, that drift is reported. What it does not
    /// cover is the runtime half: that the argument actually <em>arrives</em> in the branch's scope when the
    /// machine runs it, and that two call sites of one asset keep their arguments apart. The second is the
    /// load-bearing one, because a sub-tree asset is instantiated per call site precisely so that it can be
    /// reused, and nothing would look wrong until two branches started reading each other's values.
    /// </para>
    /// </summary>
    public class SubTreeCompositionTests : PlayModeAgentFixture
    {
        /// <summary>
        /// An argument passed at the call site is readable by name inside the branch, and the branch is the
        /// thing that actually runs.
        /// </summary>
        [UnityTest]
        public IEnumerator AnArgumentPassedAtTheCallSiteArrivesInsideTheBranch()
        {
            var branch = BuildBranch("speed", writesTo: "observedSpeed");

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);
            var call = CallBranch(graph, branch, "speed", 7.5f, 0.0f, 250.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, call);

            var machine = Spawn(tree, (_, variables) => variables.declarations.Set("observedSpeed", 0.0f));

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(7.5f, Variables.Object(machine.gameObject).Get("observedSpeed"),
                "The branch read 'speed' from its own scope and got what the call site passed. It never "
                + "learned an agent variable name, which is what lets the same branch run on any agent.");
        }

        /// <summary>
        /// Two call sites of one branch asset keep their arguments apart.
        ///
        /// <para>
        /// This is what makes a branch reusable rather than merely shared. Each
        /// <c>RunBehaviorTreeGraphNode</c> instantiates the asset and opens its own scope around that
        /// instance, so the two runs cannot see each other. Were the scope shared — or the asset run directly
        /// rather than cloned — both branches would read whichever argument was written last, and the failure
        /// would be intermittent and blamed on anything but the tree.
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator TwoCallSitesOfOneBranchKeepTheirArgumentsApart()
        {
            var branch = BuildBranch("speed", writesTo: null);

            var tree = NewTree();
            var graph = tree.graph;

            var repeater = Add<Repeater>(graph, 0.0f, 100.0f);

            // Run both at once, so neither can be "the one that happened to go last".
            var parallel = Add<ParallelSequence>(graph, 0.0f, 250.0f);
            var slow = CallBranch(graph, branch, "speed", 1.0f, -400.0f, 400.0f);
            var fast = CallBranch(graph, branch, "speed", 9.0f, 400.0f, 400.0f);

            Connect(graph, graph.EntryNode, repeater);
            Connect(graph, repeater, parallel);
            Connect(graph, parallel, slow);
            Connect(graph, parallel, fast);

            var machine = Spawn(tree);

            for (int frame = 0; frame < 4; frame++)
            {
                yield return null;
            }

            Assert.AreNotSame(slow.BehaviorTreeGraphAssetInstance, fast.BehaviorTreeGraphAssetInstance,
                "Each call site instantiates the asset, so the two runs are different objects.");

            Assert.AreEqual(1.0f, ReadBranchVariable(machine, slow, "speed"),
                "The first call site's branch sees its own argument,");
            Assert.AreEqual(9.0f, ReadBranchVariable(machine, fast, "speed"),
                "and the second sees its own -- rather than both reading whichever was written last.");
        }

        #region Building

        /// <summary>
        /// A branch asset declaring one required parameter, and optionally publishing what it read so the
        /// caller's test can see the value arrived.
        /// <para>
        /// Required rather than optional on purpose: an optional parameter carries a default the branch
        /// supplies itself, so a test using one could pass while the argument was being ignored entirely.
        /// </para>
        /// </summary>
        private static BehaviorTreeGraphAsset BuildBranch(string parameter, string writesTo)
        {
            var asset = NewTree();
            var graph = asset.graph;

            // The contract. requiredDeclarations has an internal setter, but Set on the instance is public --
            // which is the same thing BehaviorTreeAuthoring.Declare does on the editor side.
            asset.requiredDeclarations.Set(parameter, 0.0f);

            var sequence = Add<Sequence>(graph, 0.0f, 100.0f);
            Connect(graph, graph.EntryNode, sequence);

            if (!string.IsNullOrEmpty(writesTo))
            {
                var publish = Add<SetVariable>(graph, -200.0f, 250.0f);
                SetPrivateField(publish, "VariableKind", VariableKind.Object);
                FeedString(graph, publish, publish.Key, writesTo);

                // Graph scope: the branch reads its own parameter, not an agent fact.
                var read = ReadScopedVariable(graph, parameter, VariableKind.Graph, -600.0f, 400.0f);
                read.Value.ValidlyConnectTo(publish.Value);

                Connect(graph, sequence, publish);
            }

            // Something that never finishes, so the branch is still live when the test looks at it.
            var hold = Add<WaitTime>(graph, 200.0f, 250.0f);
            FeedFloat(graph, hold, hold.Time, 999.0f);
            Connect(graph, sequence, hold);

            return asset;
        }

        /// <summary>
        /// A call site: a run node pointed at the branch, its ports rebuilt from the contract, and the one
        /// parameter fed by a literal.
        /// </summary>
        private static RunBehaviorTreeGraphNode CallBranch(
            BehaviorTreeGraph graph, BehaviorTreeGraphAsset branch, string parameter, float argument,
            float x, float y)
        {
            var call = Add<RunBehaviorTreeGraphNode>(graph, x, y);
            call.SetBehaviorTreeGraphAsset(branch);

            // Ports come from a copy of the contract held on the calling node rather than read live from the
            // asset, so they only exist once this has run.
            call.RefreshParameters();

            var port = call.valueInputs.FirstOrDefault(p => p.key == parameter);
            Assert.IsNotNull(port, $"Refreshing the contract must have produced a '{parameter}' port.");

            var literal = Add<FloatLiteral>(graph, x, y + 150.0f);
            SetPrivateField(literal, "value", argument);
            literal.Value.ValidlyConnectTo(port);

            return call;
        }

        /// <summary>A Visual Scripting read of a variable in a chosen scope.</summary>
        private static GetVariable ReadScopedVariable(
            BehaviorTreeGraph graph, string key, VariableKind kind, float x, float y)
        {
            var keyLiteral = Add<StringLiteral>(graph, x - 200.0f, y);
            SetPrivateField(keyLiteral, "value", key);

            var read = Add<GetVariable>(graph, x, y);
            SetPrivateField(read, "VariableKind", kind);
            keyLiteral.Value.ValidlyConnectTo(read.Key);

            return read;
        }

        /// <summary>
        /// What a running branch holds for a name, read from the live instance the call site opened.
        /// </summary>
        private static object ReadBranchVariable(
            BehaviorTreeMachine machine, RunBehaviorTreeGraphNode authoredCall, string key)
        {
            // The authored call node is not the one running -- the machine cloned the whole tree -- so find
            // the clone by guid before asking it anything.
            var runningCall = machine.RunningGraph.Nodes
                .OfType<RunBehaviorTreeGraphNode>()
                .FirstOrDefault(n => n.guid == authoredCall.guid);

            Assert.IsNotNull(runningCall, "The running graph must contain this call site.");
            Assert.IsTrue(runningCall.HasBehaviorTreeGraphInstance,
                "and it must have instantiated its branch, or nothing was ever run.");

            return runningCall.BehaviorTreeGraphAssetInstance.declarations.Get(key);
        }

        #endregion
    }
}

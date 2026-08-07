using System.IO;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A sub-tree's contract becomes ports on the node that calls it, so a caller passes arguments instead of
    /// relying on the agent happening to declare the right names.
    /// <para>
    /// The ports are declared from a copy of the contract held on the calling node — see
    /// <see cref="BehaviorTreeGraphParameter"/> — so these cover both halves of that bargain: the ports are
    /// independent of whether the sub-tree asset is loaded, and the copy going stale is reported rather than
    /// silently unwiring the node.
    /// </para>
    /// </summary>
    public class SubTreeParameterTests
    {
        private const string Folder = "Assets/__SubTreeParameterTests";

        private string branchPath;
        private string callerPath;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__SubTreeParameterTests");

            branchPath = $"{Folder}/Branch.asset";
            callerPath = $"{Folder}/Caller.asset";
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        private RunBehaviorTreeGraphNode BuildCallerWithContract()
        {
            var branch = BehaviorTreeAuthoring.CreateTree(branchPath);
            BehaviorTreeAuthoring.Declare(branch, "attackRange", 0f, BehaviorTreeAuthoring.DeclarationScope.Required);
            BehaviorTreeAuthoring.Declare(branch, "idleTime", 3.5f, BehaviorTreeAuthoring.DeclarationScope.Optional);
            BehaviorTreeAuthoring.Save(branch);

            var caller = BehaviorTreeAuthoring.CreateTree(callerPath);
            var runNode = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0f, 200f);
            BehaviorTreeAuthoring.Connect(caller, caller.graph.EntryNode, runNode, 0);

            runNode.RefreshParameters();
            BehaviorTreeAuthoring.Save(caller);

            return runNode;
        }

        [Test]
        public void Refresh_TurnsTheContractIntoPorts()
        {
            var runNode = BuildCallerWithContract();

            var keys = runNode.valueInputs.Select(port => port.key).ToList();

            CollectionAssert.Contains(keys, "attackRange");
            CollectionAssert.Contains(keys, "idleTime");
        }

        [Test]
        public void AnOptionalParameterCarriesItsDefaultAndAnUnconnectedRequiredOneThrows()
        {
            var runNode = BuildCallerWithContract();

            Assert.AreEqual(3.5f, runNode.valueInputs.First(port => port.key == "idleTime").GetValue(),
                "an optional parameter is safe to leave unconnected");

            Assert.Throws<Unity.VisualScripting.MissingValuePortInputException>(
                () => runNode.valueInputs.First(port => port.key == "attackRange").GetValue(),
                "a required parameter must be connected, which is what makes bt_verify report it");
        }

        [Test]
        public void PortsSurviveTheRoundTripTheMachinePerforms()
        {
            BuildCallerWithContract();

            var reloaded = BehaviorTreeVerification.Reload(callerPath);
            var runNode = reloaded.graph.Nodes.OfType<RunBehaviorTreeGraphNode>().First();

            CollectionAssert.AreEquivalent(
                new[] { "attackRange", "idleTime" },
                runNode.valueInputs.Select(port => port.key).ToList());

            Assert.AreEqual(3.5f, runNode.valueInputs.First(port => port.key == "idleTime").GetValue());
        }

        [Test]
        public void VerifyReportsARequiredParameterNothingFeeds()
        {
            BuildCallerWithContract();

            var findings = BehaviorTreeVerification.Verify(callerPath);

            Assert.IsTrue(findings.Any(finding => finding.Contains("attackRange")),
                "an unfed required parameter is just an unset port, so the existing check already covers it");
        }

        [Test]
        public void DriftIsReportedBeforeAnyPortIsRebuilt()
        {
            var runNode = BuildCallerWithContract();

            Assert.IsEmpty(runNode.DescribeContractDrift(), "freshly refreshed, the copy agrees with the branch");

            // Rename the parameter on the branch, the way someone tidying up a shared branch would.
            var branch = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(branchPath);
            branch.requiredDeclarations.Clear();
            BehaviorTreeAuthoring.Declare(branch, "attackDistance", 0f, BehaviorTreeAuthoring.DeclarationScope.Required);
            BehaviorTreeAuthoring.Save(branch);

            var drift = runNode.DescribeContractDrift();

            Assert.IsTrue(drift.Any(line => line.Contains("attackDistance")), "the new name has no port yet");
            Assert.IsTrue(drift.Any(line => line.Contains("attackRange")), "and the old port is now orphaned");
            CollectionAssert.Contains(runNode.valueInputs.Select(port => port.key).ToList(), "attackRange",
                "nothing is rebuilt until someone asks, so no connection is dropped behind their back");
        }

        [Test]
        public void ACallerWithNoContractDeclaresNoParameterPorts()
        {
            var branch = BehaviorTreeAuthoring.CreateTree(branchPath);
            BehaviorTreeAuthoring.Save(branch);

            var caller = BehaviorTreeAuthoring.CreateTree(callerPath);
            var runNode = BehaviorTreeAuthoring.AddSubTree(caller, branch, 0f, 200f);
            runNode.RefreshParameters();

            Assert.IsEmpty(runNode.Parameters);
            Assert.IsEmpty(runNode.DescribeContractDrift());
        }
    }
}

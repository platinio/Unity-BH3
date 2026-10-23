#if UNITY_PIPELINE_EXIST
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    public class SetMachineTreeTests
    {
        private const string Folder = "Assets/__SetMachineTreeTests";
        private const string PrefabPath = Folder + "/Agent.prefab";
        private const string TreePath = Folder + "/Patrol.asset";

        [SetUp]
        public void MakeAnAgentAndATree()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__SetMachineTreeTests");

            BehaviorTreeAuthoring.CreateTree(TreePath);

            var agent = new GameObject("Agent", typeof(BehaviorTreeMachine));
            PrefabUtility.SaveAsPrefabAsset(agent, PrefabPath);
            Object.DestroyImmediate(agent);
        }

        [TearDown]
        public void DeleteThem()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void PointsTheMachineAtTheTree()
        {
            MachineTreeCommands.SetMachineTreeCommand(PrefabPath, TreePath);

            Assert.That(Reloaded().GetComponent<BehaviorTreeMachine>().nest.macro,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(TreePath)));
        }

        [Test]
        public void SurvivesTheRoundTripToDisk()
        {
            MachineTreeCommands.SetMachineTreeCommand(PrefabPath, TreePath);
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            Assert.That(Reloaded().GetComponent<BehaviorTreeMachine>().nest.macro, Is.Not.Null);
        }

        [Test]
        public void SaysSoWhenThereIsNoTreeAtThatPath()
        {
            Assert.That(
                () => MachineTreeCommands.SetMachineTreeCommand(PrefabPath, Folder + "/Missing.asset"),
                Throws.ArgumentException.With.Message.Contains("Missing"));
        }

        [Test]
        public void SaysSoWhenTheObjectHasNoMachine()
        {
            var plain = new GameObject("Plain");
            PrefabUtility.SaveAsPrefabAsset(plain, Folder + "/Plain.prefab");
            Object.DestroyImmediate(plain);

            Assert.That(
                () => MachineTreeCommands.SetMachineTreeCommand(Folder + "/Plain.prefab", TreePath),
                Throws.ArgumentException.With.Message.Contains("BehaviorTreeMachine"));
        }

        private static GameObject Reloaded() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }
}
#endif

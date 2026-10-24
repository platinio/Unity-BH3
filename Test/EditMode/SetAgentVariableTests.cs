#if UNITY_PIPELINE_EXIST
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    public class SetAgentVariableTests
    {
        private const string Folder = "Assets/__SetAgentVariableTests";
        private const string PrefabPath = Folder + "/Agent.prefab";

        [SetUp]
        public void MakeAnAgentPrefab()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__SetAgentVariableTests");

            var agent = new GameObject("Agent", typeof(Variables));
            PrefabUtility.SaveAsPrefabAsset(agent, PrefabPath);
            Object.DestroyImmediate(agent);
        }

        [TearDown]
        public void DeleteThePrefab()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void WritesANumberTheTreeCanRead()
        {
            AgentVariableCommands.SetAgentVariableCommand(PrefabPath, "keepDistance", JToken.Parse("3.5"));

            Assert.That(Declarations().Get("keepDistance"), Is.EqualTo(3.5f));
        }

        [Test]
        public void WritesABool()
        {
            AgentVariableCommands.SetAgentVariableCommand(PrefabPath, "isGuard", JToken.Parse("true"));

            Assert.That(Declarations().Get("isGuard"), Is.True);
        }

        [Test]
        public void WritesAVector3FromThreeNumbers()
        {
            AgentVariableCommands.SetAgentVariableCommand(PrefabPath, "post", JToken.Parse("[1, 2, 3]"));

            Assert.That(Declarations().Get("post"), Is.EqualTo(new Vector3(1.0f, 2.0f, 3.0f)));
        }

        [Test]
        public void ReplacesAValueItHasAlreadyWritten()
        {
            AgentVariableCommands.SetAgentVariableCommand(PrefabPath, "keepDistance", JToken.Parse("3"));
            AgentVariableCommands.SetAgentVariableCommand(PrefabPath, "keepDistance", JToken.Parse("6"));

            Assert.That(Declarations().Get("keepDistance"), Is.EqualTo(6.0f));
            Assert.That(Declarations().Count(), Is.EqualTo(1));
        }

        [Test]
        public void SaysSoWhenTheAgentHasNoVariablesComponent()
        {
            var plain = new GameObject("Plain");
            PrefabUtility.SaveAsPrefabAsset(plain, Folder + "/Plain.prefab");
            Object.DestroyImmediate(plain);

            Assert.That(
                () => AgentVariableCommands.SetAgentVariableCommand(Folder + "/Plain.prefab", "keepDistance", JToken.Parse("3")),
                Throws.ArgumentException.With.Message.Contains("Variables"));
        }

        private static VariableDeclarations Declarations() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Variables>().declarations;
    }
}
#endif

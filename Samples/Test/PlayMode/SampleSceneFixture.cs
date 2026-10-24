using System;
using System.Collections;
using System.Linq;
using ArcaneOnyx.BehaviorTree;
using ArcaneOnyx.BehaviorTree.Debugging;
using ArcaneOnyx.BehaviorTree.Tests.PlayMode;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ArcaneOnyx.BH3.Samples.Tests.PlayMode
{
    public abstract class SampleSceneFixture : PlayModeAgentFixture
    {
        protected const float FrameSeconds = 0.05f;
        private const string SamplesFolder = "Assets/ArcaneOnyx/BH3/Samples/";

        private Scene scene;

        protected abstract string SampleName { get; }

        [UnitySetUp]
        public IEnumerator LoadTheSampleScene()
        {
#if UNITY_EDITOR
            Application.runInBackground = true;
            Time.captureDeltaTime = FrameSeconds;
            BehaviorTreeFlightRecorders.GloballyEnabled = true;
            scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                SamplesFolder + SampleName + "/" + SampleName + ".unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            SceneManager.SetActiveScene(scene);
#else
            Assert.Ignore("Sample scenes are loaded by asset path, which only the Editor can do.");
            yield break;
#endif
        }

        [UnityTearDown]
        public IEnumerator UnloadTheSampleScene()
        {
            Time.captureDeltaTime = 0f;
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        protected static IEnumerator Seconds(float seconds) => Frames(Mathf.CeilToInt(seconds / FrameSeconds));

        protected static IEnumerator Until(Func<bool> condition, float timeoutSeconds, string whatWasExpected)
        {
            int framesLeft = Mathf.CeilToInt(timeoutSeconds / FrameSeconds);
            while (!condition())
            {
                if (framesLeft-- <= 0) Assert.Fail(whatWasExpected + " did not happen within " + timeoutSeconds + " seconds.");
                yield return null;
            }
        }

        protected static T TheOnly<T>() where T : UnityEngine.Object
        {
            T found = UnityEngine.Object.FindFirstObjectByType<T>();
            Assert.IsNotNull(found, "The scene has no " + typeof(T).Name + ".");
            return found;
        }

        protected static Guid NodeIn(string sampleName, string treeName, string nodeName)
        {
#if UNITY_EDITOR
            string path = SamplesFolder + sampleName + "/Trees/" + treeName + ".asset";
            var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(path);
            Assert.IsNotNull(tree, "There is no tree at " + path + ".");
            BehaviorTreeNode node = tree.graph.Nodes.FirstOrDefault(candidate => candidate != null && candidate.IsVisible && candidate.NodeName == nodeName);
            Assert.IsNotNull(node, treeName + " has no node named '" + nodeName + "'.");
            return node.guid;
#else
            throw new NotSupportedException("Tree assets are read by path, which only the Editor can do.");
#endif
        }

        protected static BehaviorTreeFlightRecorder RecorderOf(Component agent) =>
            agent.GetComponent<BehaviorTreeMachine>().FlightRecorder;

        protected static void Silence(Component agent) => agent.GetComponent<BehaviorTreeMachine>().enabled = false;

        protected static void Publish(Component agent, string key, object value) =>
            AgentVariableWriter.SetOn(agent.gameObject, key, value);

        protected static object Fact(Component agent, string key)
        {
            VariableDeclarations facts = Variables.Object(agent.gameObject);
            return facts.IsDefined(key) ? facts.Get(key) : null;
        }

        protected static int EntriesAtLeast(BehaviorTreeFlightRecorder recorder, Guid node) =>
            recorder.Events.Count(recorded => recorded.Kind == BehaviorTreeEventKind.NodeEnter && recorded.NodeGuid == node);

        protected static void Teleport(Component agent, Vector3 to)
        {
            if (agent.TryGetComponent(out NavMeshAgent navigation) && navigation.enabled && navigation.isOnNavMesh) navigation.Warp(to);
            else agent.transform.position = to;
        }

        protected static Vector3 OnTheNavMesh(Vector3 near)
        {
            Assert.IsTrue(NavMesh.SamplePosition(near, out NavMeshHit hit, 3f, NavMesh.AllAreas), "No NavMesh near " + near + ".");
            return hit.position;
        }

        protected static float FlatDistance(Vector3 from, Vector3 to)
        {
            from.y = 0f;
            to.y = 0f;
            return Vector3.Distance(from, to);
        }

        protected static float FlatAngleBetween(Transform looker, Vector3 point)
        {
            Vector3 toPoint = point - looker.position;
            toPoint.y = 0f;
            return Vector3.Angle(looker.forward, toPoint);
        }
    }
}

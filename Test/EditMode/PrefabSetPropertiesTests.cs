#if UNITY_PIPELINE_EXIST
using ArcaneOnyx.BehaviorTree.Authoring;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// <c>prefab_set_properties</c> writes a prefab asset where it lies, so a value is proven by reloading
    /// the asset from disk rather than by reading the object the command happened to touch.
    /// </summary>
    public class PrefabSetPropertiesTests
    {
        private const string Folder = "Assets/__PrefabSetPropertiesTests";
        private const string PrefabPath = Folder + "/Lamp.prefab";
        private const string CookiePath = Folder + "/Cookie.asset";

        [SetUp]
        public void MakeAPrefabWithAChild()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__PrefabSetPropertiesTests");

            AssetDatabase.CreateAsset(new Texture2D(4, 4), CookiePath);

            var lamp = new GameObject("Lamp", typeof(Light));
            lamp.GetComponent<Light>().type = LightType.Spot;
            var shade = new GameObject("Shade", typeof(BoxCollider));
            shade.transform.SetParent(lamp.transform, false);

            PrefabUtility.SaveAsPrefabAsset(lamp, PrefabPath);
            Object.DestroyImmediate(lamp);
        }

        [TearDown]
        public void DeleteThePrefab()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void WritesNumbersAndColoursOntoTheRoot()
        {
            PrefabAuthoringCommands.SetPrefabPropertiesCommand(
                PrefabPath, "Light", JObject.Parse("{\"m_Intensity\": 2.5, \"m_Range\": 12, \"m_Color\": [1, 0, 0, 1]}"));

            var light = Reloaded().GetComponent<Light>();

            Assert.That(light.intensity, Is.EqualTo(2.5f));
            Assert.That(light.range, Is.EqualTo(12.0f));
            Assert.That(light.color, Is.EqualTo(Color.red));
        }

        [Test]
        public void WritesOntoAChildWhenOneIsNamed()
        {
            PrefabAuthoringCommands.SetPrefabPropertiesCommand(
                PrefabPath, "BoxCollider", JObject.Parse("{\"m_IsTrigger\": true, \"m_Size\": [2, 3, 4]}"), "Shade");

            var box = Reloaded().transform.Find("Shade").GetComponent<BoxCollider>();

            Assert.That(box.isTrigger, Is.True);
            Assert.That(box.size, Is.EqualTo(new Vector3(2.0f, 3.0f, 4.0f)));
        }

        [Test]
        public void PointsAnObjectReferenceAtTheAssetAtThatPath()
        {
            PrefabAuthoringCommands.SetPrefabPropertiesCommand(
                PrefabPath, "Light", JObject.Parse("{\"m_Cookie\": \"" + CookiePath + "\"}"));

            Assert.That(Reloaded().GetComponent<Light>().cookie,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(CookiePath)));
        }

        [Test]
        public void WritesAnEnumByName()
        {
            PrefabAuthoringCommands.SetPrefabPropertiesCommand(
                PrefabPath, "Light", JObject.Parse("{\"m_Type\": \"Directional\"}"));

            Assert.That(Reloaded().GetComponent<Light>().type, Is.EqualTo(LightType.Directional));
        }

        [Test]
        public void SaysWhatTheComponentHasWhenTheTypeIsNotOnIt()
        {
            Assert.That(
                () => PrefabAuthoringCommands.SetPrefabPropertiesCommand(PrefabPath, "Rigidbody", JObject.Parse("{\"m_Mass\": 2}")),
                Throws.ArgumentException.With.Message.Contains("Light"));
        }

        [Test]
        public void SaysWhichPropertyItCouldNotFind()
        {
            Assert.That(
                () => PrefabAuthoringCommands.SetPrefabPropertiesCommand(PrefabPath, "Light", JObject.Parse("{\"brightness\": 2}")),
                Throws.ArgumentException.With.Message.Contains("brightness"));
        }

        [Test]
        public void LeavesTheChildrenItWasNotAskedAbout()
        {
            PrefabAuthoringCommands.SetPrefabPropertiesCommand(
                PrefabPath, "Light", JObject.Parse("{\"m_Intensity\": 3}"));

            Assert.That(Reloaded().transform.Find("Shade"), Is.Not.Null);
            Assert.That(Reloaded().transform.Find("Shade").GetComponent<BoxCollider>().isTrigger, Is.False);
        }

        private static GameObject Reloaded() => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }
}
#endif

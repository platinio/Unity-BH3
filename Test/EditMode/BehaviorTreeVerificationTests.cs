using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Pins the reload behaviour that <see cref="BehaviorTreeVerification"/> exists for, and with it the
    /// serialization rule that motivates <see cref="BehaviorTreeAuthoring.SetValue"/>: an inline value only
    /// survives on a port whose Definition declared one.
    /// <para>
    /// These write real assets, because the whole point is what happens when one comes back from disk.
    /// </para>
    /// </summary>
    [TestFixture]
    public class BehaviorTreeVerificationTests
    {
        private const string Folder = "Assets/BehaviorTreeVerificationTests_Temp";
        private const string TreePath = Folder + "/Fixture.asset";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "BehaviorTreeVerificationTests_Temp");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void InlineValueOnABarePortIsLostAcrossAReload()
        {
            // WaitTime.Time is declared as ValueInput<float>(nameof(Time)) — no default — so this is the
            // trap: it holds in memory and vanishes on reload, with nothing warning you in between.
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, wait);

            wait.Time.SetDefaultValue(1.5f);
            Assert.IsTrue(wait.Time.behaviorTreeNode.defaultValues.ContainsKey("Time"),
                "In the generating run the value is present, which is why a same-run dump cannot catch this.");

            BehaviorTreeAuthoring.Save(asset);
            var reloaded = BehaviorTreeVerification.Reload(TreePath);

            var reloadedWait = reloaded.graph.Nodes.OfType<WaitTime>().Single();
            Assert.IsFalse(reloadedWait.defaultValues.ContainsKey("Time"),
                "A bare port cannot hold an inline value across serialization — this is why SetValue exists.");
        }

        [Test]
        public void VerifyReportsAPortThatWillThrow()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, wait);

            wait.Time.SetDefaultValue(1.5f);   // the mistake
            BehaviorTreeAuthoring.Save(asset);

            var findings = BehaviorTreeVerification.Verify(TreePath);

            Assert.IsTrue(findings.Any(f => f.Contains("Time")),
                "The verifier's whole purpose is catching this one, so it must not come back clean:\n  " +
                string.Join("\n  ", findings));
        }

        [Test]
        public void SetValueSurvivesAReload()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, wait);

            BehaviorTreeAuthoring.SetValue(asset, wait.Time, 1.5f, -200.0f, 100.0f);
            BehaviorTreeAuthoring.Save(asset);

            var reloaded = BehaviorTreeVerification.Reload(TreePath);
            var reloadedWait = reloaded.graph.Nodes.OfType<WaitTime>().Single();

            Assert.IsTrue(reloadedWait.Time.hasValidConnection,
                "SetValue must route a bare port to a connected literal, which is what survives.");
            Assert.IsEmpty(BehaviorTreeVerification.Verify(TreePath),
                "A tree built with SetValue should verify clean.");
        }

        [Test]
        public void SetValueUsesAnInlineValueWhenThePortDeclaresOne()
        {
            // SetAnimatorTrigger.TriggerName is declared with a null default, so the key exists and an
            // inline value persists — no literal node needed, and none should be added.
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var play = BehaviorTreeAuthoring.AddNode<SetAnimatorTrigger>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, play);

            BehaviorTreeAuthoring.SetValue(asset, play.TriggerName, "Attack", -200.0f, 100.0f);
            BehaviorTreeAuthoring.Save(asset);

            var reloaded = BehaviorTreeVerification.Reload(TreePath);
            var reloadedPlay = reloaded.graph.Nodes.OfType<SetAnimatorTrigger>().Single();

            Assert.AreEqual("Attack", reloadedPlay.defaultValues["TriggerName"]);
            Assert.IsFalse(reloaded.graph.Nodes.OfType<StringLiteral>().Any(),
                "A port that can hold an inline value should not gain a literal node it does not need.");
        }
    }
}

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
            // BarePortNode.Position is declared as ValueInput<Vector3>(nameof(Position)) — no
            // default — so this is the trap: it holds in memory and vanishes on reload, with nothing
            // warning you in between. (A test double on purpose: WaitTime.Time and then
            // SetNavAgentPosition.NavPosition were the examples here, and each gained a default so
            // designers can type a value on the canvas.)
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var move = BehaviorTreeAuthoring.AddNode<BarePortNode>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, move);

            move.Position.SetDefaultValue(UnityEngine.Vector3.one);
            Assert.IsTrue(move.Position.behaviorTreeNode.defaultValues.ContainsKey("Position"),
                "In the generating run the value is present, which is why a same-run dump cannot catch this.");

            BehaviorTreeAuthoring.Save(asset);
            var reloaded = BehaviorTreeVerification.Reload(TreePath);

            var reloadedMove = reloaded.graph.Nodes.OfType<BarePortNode>().Single();
            Assert.IsFalse(reloadedMove.defaultValues.ContainsKey("Position"),
                "A bare port cannot hold an inline value across serialization — this is why SetValue exists.");
        }

        /// <summary>
        /// The other half of the same rule, and the reason the wait nodes were changed: a port that
        /// <i>does</i> declare a default keeps what was typed into it, so the inline field the canvas
        /// offers is honest.
        /// </summary>
        [Test]
        public void InlineValueOnADeclaredDefaultPortSurvivesAReload()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var wait = BehaviorTreeAuthoring.AddNode<WaitTime>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, wait);

            wait.Time.SetDefaultValue(1.5f);
            BehaviorTreeAuthoring.Save(asset);

            var reloaded = BehaviorTreeVerification.Reload(TreePath);
            var reloadedWait = reloaded.graph.Nodes.OfType<WaitTime>().Single();

            Assert.AreEqual(1.5f, reloadedWait.defaultValues["Time"],
                "Definition declares Time's default, so Definition puts the typed value back on reload.");
            Assert.IsEmpty(BehaviorTreeVerification.Verify(TreePath),
                "A declared default is fed by definition, so it is not an unset port.");
        }

        /// <summary>
        /// A node whose type was deleted comes back as <see cref="MissingType"/>, and the tree loads as if
        /// nothing happened -- wiring intact, no exception, the branch silently shorter. Deleting a node
        /// type is how one is retired here (RunScriptGraph was the first), so this is the only thing
        /// standing between "we removed it" and a downstream tree that quietly stopped doing something.
        /// </summary>
        [Test]
        public void VerifyNamesANodeWhoseTypeNoLongerExists()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var sequence = BehaviorTreeAuthoring.AddNode<Sequence>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, sequence);

            // The placeholder itself, as the asset would produce it for an unknown $type.
            var missing = BehaviorTreeAuthoring.AddNode<MissingType>(asset, 0.0f, 300.0f);
            BehaviorTreeAuthoring.Connect(asset, sequence, missing);

            BehaviorTreeAuthoring.Save(asset);

            var findings = BehaviorTreeVerification.Verify(TreePath);

            Assert.IsTrue(findings.Any(f => f.Contains("no longer exists")),
                "a node standing in for a deleted type must be named, or the tree passes verification while "
                + "silently doing less than it did: " + string.Join(" | ", findings));
        }

        [Test]
        public void VerifyReportsAPortThatWillThrow()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var move = BehaviorTreeAuthoring.AddNode<BarePortNode>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, move);

            move.Position.SetDefaultValue(UnityEngine.Vector3.one);   // the mistake
            BehaviorTreeAuthoring.Save(asset);

            var findings = BehaviorTreeVerification.Verify(TreePath);

            Assert.IsTrue(findings.Any(f => f.Contains("Position")),
                "The verifier's whole purpose is catching this one, so it must not come back clean:\n  " +
                string.Join("\n  ", findings));
        }

        [Test]
        public void SetValueSurvivesAReload()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var move = BehaviorTreeAuthoring.AddNode<BarePortNode>(asset, 0.0f, 100.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, move);

            BehaviorTreeAuthoring.SetValue(asset, move.Position, UnityEngine.Vector3.one, -200.0f, 100.0f);
            BehaviorTreeAuthoring.Save(asset);

            var reloaded = BehaviorTreeVerification.Reload(TreePath);
            var reloadedMove = reloaded.graph.Nodes.OfType<BarePortNode>().Single();

            Assert.IsTrue(reloadedMove.Position.hasValidConnection,
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

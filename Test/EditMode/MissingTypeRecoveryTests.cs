using System.IO;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Pins what survives when a node type is deleted or renamed.
    ///
    /// <para>
    /// These write real assets and break them on disk, because the whole mechanism lives in the gap between
    /// what the file says and what the serializer can resolve — and that gap only exists on a genuine load.
    /// A test that constructs a placeholder in memory would pass while the thing it describes was broken.
    /// </para>
    /// </summary>
    [TestFixture]
    public class MissingTypeRecoveryTests
    {
        private const string Folder = "Assets/MissingTypeRecoveryTests_Temp";
        private const string TreePath = Folder + "/Fixture.asset";
        private const string SubTreePath = Folder + "/SubTree.asset";

        /// <summary>A name no type has, so the serializer cannot resolve it.</summary>
        private const string GoneTypeName = "ArcaneOnyx.BehaviorTree.Tests.NoSuchNodeType";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "MissingTypeRecoveryTests_Temp");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        // ------------------------------------------------------------------ helpers

        private static string DiskPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        /// <summary>
        /// Rewrites a type name inside the saved asset and forces it back through a real load — the only way
        /// to produce the state a deleted script produces.
        /// </summary>
        private static BehaviorTreeGraphAsset RewriteTypeAndReload(string assetPath, string from, string to)
        {
            AssetDatabase.SaveAssets();

            string disk = DiskPath(assetPath);
            string text = File.ReadAllText(disk);

            Assert.IsTrue(text.Contains(from),
                $"the fixture must actually contain '{from}', or this test is asserting about nothing.");

            File.WriteAllText(disk, text.Replace(from, to));

            AssetDatabase.ImportAsset(assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(assetPath);
        }

        private static string TypeKey(string typeName) => "\"$type\":\"" + typeName + "\"";

        private static MissingType SinglePlaceholder(BehaviorTreeGraphAsset asset)
        {
            var placeholders = asset.graph.Nodes.OfType<MissingType>().ToList();

            Assert.AreEqual(1, placeholders.Count,
                "expected exactly one placeholder: " + string.Join(", ", asset.graph.Nodes.Select(n => n.GetType().Name)));

            return placeholders[0];
        }

        // ------------------------------------------------------------------ the placeholder keeps things

        [Test]
        public void ADeletedTypeLeavesAPlaceholderThatKeepsWhatTheNodeHeld()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);

            source.Speed = 7.5f;
            source.Label = "carried";
            BehaviorTreeAuthoring.Save(asset);

            var sourceGuid = source.guid;
            var sourcePosition = source.Position;

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);

            Assert.AreEqual(GoneTypeName, placeholder.formerType,
                "without the former type name nothing downstream can offer a fix, which is the whole feature.");
            Assert.IsTrue(placeholder.HasPreservedState, "the node's data must be kept, not merely noted as lost.");
            Assert.AreEqual(sourceGuid, placeholder.guid, "the placeholder stands in for the node, so it is the node's identity.");
            Assert.AreEqual(sourcePosition, placeholder.Position);

            StringAssert.Contains("carried", placeholder.formerValue,
                "the preserved document must contain the node's own values, not just its base members.");

            Assert.AreEqual(1, broken.graph.Transitions.Count(t => t.destination == placeholder),
                "the wiring around a broken node has to survive, or recovering the node recovers nothing.");
        }

        [Test]
        public void APlaceholderSurvivesBeingSavedAndLoadedAgain()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            source.Label = "carried";
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            string preserved = SinglePlaceholder(broken).formerValue;

            // The save is the moment the old data would be dropped if the placeholder did not hold it: the
            // document is rewritten from the live objects, and the missing type's members have no owner.
            EditorUtility.SetDirty(broken);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TreePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            var reloaded = AssetDatabase.LoadAssetAtPath<BehaviorTreeGraphAsset>(TreePath);
            var placeholder = SinglePlaceholder(reloaded);

            Assert.AreEqual(GoneTypeName, placeholder.formerType);
            Assert.AreEqual(preserved, placeholder.formerValue,
                "a placeholder that loses its state on the first save recovers nothing the second time.");
        }

        [Test]
        public void AHealthyTreeIsNotRewrittenAtAll()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            var reloaded = BehaviorTreeVerification.Reload(TreePath);

            Assert.IsFalse(MissingTypeRecovery.HasPlaceholder(reloaded.graph),
                "the converter must leave a tree whose types all resolve exactly as it found it.");
            Assert.AreEqual(1, reloaded.graph.Nodes.OfType<RetargetSourceNode>().Count());
        }

        [Test]
        public void NodesAreTheOnlyThingReplacedByAPlaceholder()
        {
            // A placeholder is a node, so it can only stand in for a node. The rewrite this replaced was
            // blanket: it turned a deleted *value* type -- a graph variable's value, a member of a
            // [Serializable] class -- into a node type too, which is never assignable to the slot it was
            // written for and so could only ever fail. This pins what a deleted non-node type does instead,
            // because "what happens when a type disappears" is the one thing this feature must not leave to
            // assumption.
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Declare(asset, "ammo", 7);
            BehaviorTreeAuthoring.Save(asset);

            // The declaration's value type, not any node's type.
            var loaded = RewriteTypeAndReload(
                TreePath, TypeKey("System.Int32"), TypeKey("ArcaneOnyx.BehaviorTree.Tests.NoSuchValueType"));

            Assert.IsFalse(MissingTypeRecovery.HasPlaceholder(loaded.graph),
                "a node placeholder in a value slot could only ever fail, so nothing should have been "
                + "replaced here.");

            Assert.AreEqual(1, loaded.graph.Nodes.OfType<RetargetSourceNode>().Count(),
                "the rest of the tree has to survive a deleted value type: " + string.Join(
                    ", ", loaded.graph.Nodes.Select(n => n.GetType().Name)));
            Assert.AreEqual(1, loaded.graph.Transitions.Count, "and so does its wiring.");

            var declaration = loaded.declarations.SingleOrDefault(d => d.name == "ammo");
            Assert.IsNotNull(declaration, "the declaration itself survives; only the value it held cannot.");
            Assert.IsNull(declaration.value,
                "a value whose type is gone degrades to null -- which is recoverable by retyping it, unlike "
                + "an asset that failed to load.");
        }

        // ------------------------------------------------------------------ restoring itself

        [Test]
        public void APlaceholderRestoresItselfWhenItsTypeExistsAgain()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            source.Speed = 3.25f;
            source.Label = "restored";
            BehaviorTreeAuthoring.Save(asset);

            var sourceGuid = source.guid;

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            Assert.IsNotNull(SinglePlaceholder(broken));

            EditorUtility.SetDirty(broken);
            AssetDatabase.SaveAssets();

            // The script coming back is exactly this: the name the placeholder remembers resolves again.
            var healed = RewriteTypeAndReload(TreePath, GoneTypeName, typeof(RetargetSourceNode).FullName);

            Assert.IsFalse(MissingTypeRecovery.HasPlaceholder(healed.graph),
                "re-adding the script must heal the tree with no further action -- that is the whole point.");

            var restored = healed.graph.Nodes.OfType<RetargetSourceNode>().Single();

            Assert.AreEqual(sourceGuid, restored.guid, "the restored node is the same node, not a new one.");
            Assert.AreEqual(3.25f, restored.Speed, 0.0001f, "values the node held must come back with it.");
            Assert.AreEqual("restored", restored.Label);
            Assert.AreEqual(1, healed.graph.Transitions.Count(t => t.destination == restored),
                "a restored node that nothing points at is not restored.");
        }

        [Test]
        public void ARestoredNodeKeepsItsUnityObjectReferences()
        {
            // The case the preserved object table exists for. A Unity object inside a node serializes as a
            // bare index into the asset's table, and that table is rebuilt on every save -- so a naive
            // implementation restores the node pointing at the wrong asset, or at nothing.
            var subTree = BehaviorTreeAuthoring.CreateTree(SubTreePath);
            BehaviorTreeAuthoring.Save(subTree);

            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var runner = BehaviorTreeAuthoring.AddNode<RunBehaviorTreeGraphNode>(asset, 0.0f, 200.0f);
            runner.SetBehaviorTreeGraphAsset(subTree);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, runner);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RunBehaviorTreeGraphNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);

            Assert.IsNotNull(placeholder.formerObjects, "the object table must be captured, or the reference is unrecoverable.");
            CollectionAssert.Contains(placeholder.formerObjects, subTree,
                "the asset the node pointed at has to be held by the placeholder to survive the next save.");

            // Save first, so the restore is reading a table that has already been through a round trip.
            EditorUtility.SetDirty(broken);
            AssetDatabase.SaveAssets();

            var healed = RewriteTypeAndReload(TreePath, GoneTypeName, typeof(RunBehaviorTreeGraphNode).FullName);
            var restored = healed.graph.Nodes.OfType<RunBehaviorTreeGraphNode>().Single();

            Assert.AreEqual(subTree, restored.BehaviorTreeGraphAsset,
                "the restored node must point at the same sub-tree it did before, not at whatever now sits "
                + "at that index in a rebuilt object table.");
        }

        [Test]
        public void RenamedFromResolvesAtLoadWithoutLeavingAPlaceholder()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var node = BehaviorTreeAuthoring.AddNode<RenamedNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, node);
            BehaviorTreeAuthoring.Save(asset);

            // The name RenamedNode declares it used to have. Nothing in this project defines it, so the only
            // thing that can resolve it is the attribute.
            var loaded = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RenamedNode).FullName), TypeKey(RenamedNode.PreviousName));

            Assert.IsFalse(MissingTypeRecovery.HasPlaceholder(loaded.graph),
                "[RenamedFrom] is the durable fix this feature points people at, so it must actually work.");
            Assert.AreEqual(1, loaded.graph.Nodes.OfType<RenamedNode>().Count());
        }

        // ------------------------------------------------------------------ retargeting by hand

        [Test]
        public void RetargetingCarriesWhatFitsAndStrandsWhatDoesNot()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);

            source.Speed = 9.5f;
            source.Label = "dropped by the target";

            // Feed both ports, so the test can tell a connection that lands from one that is stranded.
            BehaviorTreeAuthoring.FeedFloat(asset, source.Alpha, 1.0f, -300.0f, 150.0f);
            BehaviorTreeAuthoring.FeedFloat(asset, source.Beta, 2.0f, -300.0f, 300.0f);
            BehaviorTreeAuthoring.Save(asset);

            var sourceGuid = source.guid;

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);

            var replacement = MissingTypeRecovery.Retarget(
                broken.graph, placeholder, typeof(RetargetTargetNode), out var failure);

            Assert.IsNotNull(replacement, "retarget failed: " + failure);

            var target = (RetargetTargetNode)replacement;

            Assert.AreEqual(sourceGuid, target.guid, "the replacement stands where the old node stood.");
            Assert.AreEqual(9.5f, target.Speed, 0.0001f,
                "a member the replacement also declares carries over -- that is what makes a rename cheap.");
            Assert.AreEqual(1, broken.graph.Transitions.Count(t => t.destination == target),
                "the transition that pointed at the placeholder must point at the replacement.");

            Assert.IsTrue(target.Alpha.hasAnyConnection,
                "a port the replacement still declares keeps what was wired to it.");
            Assert.IsTrue(target.invalidInputs.Any(port => port.key == nameof(RetargetSourceNode.Beta)),
                "a port the replacement does not declare must survive as an invalid port, so the author can "
                + "see what was wired there rather than silently losing it.");

            Assert.IsFalse(MissingTypeRecovery.HasPlaceholder(broken.graph), "the placeholder is gone once replaced.");
        }

        [Test]
        public void ThePreviewAgreesWithWhatRetargetingActuallyDoes()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            source.Label = "gone after retarget";
            BehaviorTreeAuthoring.FeedFloat(asset, source.Beta, 2.0f, -300.0f, 300.0f);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);
            var preview = MissingTypeRetarget.PreviewRetarget(placeholder, typeof(RetargetTargetNode));

            Assert.IsTrue(preview.CanApply, preview.Failure);
            CollectionAssert.Contains(preview.KeptMembers, nameof(RetargetSourceNode.Speed));
            CollectionAssert.Contains(preview.DroppedMembers, nameof(RetargetSourceNode.Label));
            CollectionAssert.Contains(preview.StrandedConnections, nameof(RetargetSourceNode.Beta));
        }

        [Test]
        public void AnInlineValueTypedIntoAPortCarriesOverAndIsShownInThePreview()
        {
            // A value typed straight into a port lives in defaultValues rather than among the node's
            // members, so it is invisible to anything reasoning about members alone. An author whose node
            // was all port literals would otherwise be told "keeps 0 values" while their values quietly
            // carried or vanished.
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);

            // Tuning and Legacy, because they declare defaults -- SetValue only writes an inline value on a
            // port that does, and wires a literal node into any port that does not. On a bare port there is
            // no inline value to carry in the first place.
            BehaviorTreeAuthoring.SetValue(asset, source.Tuning, 4.5f, -300.0f, 150.0f);
            BehaviorTreeAuthoring.SetValue(asset, source.Legacy, 6.5f, -300.0f, 300.0f);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);

            var preview = MissingTypeRetarget.PreviewRetarget(placeholder, typeof(RetargetTargetNode));

            Assert.IsTrue(preview.CanApply, preview.Failure);
            Assert.IsTrue(preview.KeptMembers.Any(m => m.Contains(nameof(RetargetSourceNode.Tuning))),
                "an inline value on a port the replacement still declares carries over, and the preview has "
                + "to say so: " + string.Join(", ", preview.KeptMembers));
            Assert.IsTrue(preview.DroppedMembers.Any(m => m.Contains(nameof(RetargetSourceNode.Legacy))),
                "and one on a port it does not declare is lost, which is exactly what an author needs told "
                + "before committing: " + string.Join(", ", preview.DroppedMembers));

            var replacement = (RetargetTargetNode)MissingTypeRecovery.Retarget(
                broken.graph, placeholder, typeof(RetargetTargetNode), out var failure);

            Assert.IsNotNull(replacement, "retarget failed: " + failure);
            Assert.AreEqual(4.5f, replacement.defaultValues[nameof(RetargetTargetNode.Tuning)],
                "the preview promised this value would carry, so it must actually carry.");
            Assert.IsFalse(replacement.defaultValues.ContainsKey(nameof(RetargetSourceNode.Legacy)),
                "and the one it said would be dropped must not silently reappear.");
        }

        [Test]
        public void RetargetingRefusesAPlaceholderThatIsNoLongerInTheTree()
        {
            // The picker is a non-modal window holding a placeholder across arbitrary editor time. A reimport
            // swaps the graph out from under it with no domain reload to null the reference, leaving a stale
            // placeholder that looks fine and shares its guid with the live one. Nodes is keyed by guid, so
            // proceeding is either a duplicate-key throw mid-swap or a rebuild from state nobody is looking
            // at.
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var stale = SinglePlaceholder(broken);

            // A second, independent load of the same asset: same guid, different object -- exactly what the
            // window is left holding after a reimport.
            var reloaded = BehaviorTreeVerification.Reload(TreePath);
            var live = reloaded.graph.Nodes.OfType<MissingType>().Single();

            Assert.AreEqual(stale.guid, live.guid, "the fixture is only meaningful if the guids match.");
            Assert.AreNotSame(stale, live, "and only if the objects do not.");

            var replacement = MissingTypeRecovery.Retarget(
                reloaded.graph, stale, typeof(RetargetTargetNode), out var failure);

            Assert.IsNull(replacement, "a placeholder the graph does not hold must be refused, not applied.");
            StringAssert.Contains("no longer in the tree", failure,
                "and the refusal has to say what to do about it: " + failure);
            Assert.IsTrue(MissingTypeRecovery.HasPlaceholder(reloaded.graph),
                "the live graph must be left exactly as it was.");
        }

        [Test]
        public void RetargetingRefusesATypeThatIsNotANode()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);

            var replacement = MissingTypeRecovery.Retarget(
                broken.graph, placeholder, typeof(string), out var failure);

            Assert.IsNull(replacement, "a non-node must be refused rather than half-applied.");
            Assert.IsNotNull(failure, "a refusal has to say why, or the caller can only report that it failed.");
            Assert.IsTrue(MissingTypeRecovery.HasPlaceholder(broken.graph),
                "a refused retarget must leave the placeholder untouched.");
        }

        [Test]
        public void RetargetingATreeConvertsEveryPlaceholderOfThatType()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);

            var first = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            var second = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 200.0f, 200.0f);
            var untouched = BehaviorTreeAuthoring.AddNode<RenamedNode>(asset, 400.0f, 200.0f);

            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, first);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, second);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, untouched);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            Assert.AreEqual(2, broken.graph.Nodes.OfType<MissingType>().Count());

            int converted = MissingTypeRetarget.ApplyToTree(broken, GoneTypeName, typeof(RetargetTargetNode));

            Assert.AreEqual(2, converted, "one rename is one decision, however many nodes it touched.");
            Assert.AreEqual(2, broken.graph.Nodes.OfType<RetargetTargetNode>().Count());
            Assert.IsFalse(MissingTypeRecovery.HasPlaceholder(broken.graph));
            Assert.AreEqual(1, broken.graph.Nodes.OfType<RenamedNode>().Count(),
                "a node of another type must not be touched by a retarget aimed at this one.");
        }

        [Test]
        public void TheReplacementTypeListOffersTheTypeThatSharesTheMissingName()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            // A type moved to another namespace keeps its short name, which is the case worth ranking first.
            string movedName = "SomeOtherNamespace." + nameof(RetargetTargetNode);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(movedName));

            var candidates = MissingTypeRetarget.CandidateTypes(SinglePlaceholder(broken));

            CollectionAssert.IsNotEmpty(candidates);
            Assert.AreEqual(typeof(RetargetTargetNode), candidates[0],
                "a type whose short name matches the one that went missing is nearly always the answer, so "
                + "it has to be the first thing offered.");
        }

        [Test]
        public void MovingAPlaceholderBeforeRecoveringItKeepsWhereItWasMovedTo()
        {
            // Dragging the red node somewhere it can be read is the first thing anyone does with a broken
            // tree. The preserved document froze the node's position at the moment its type went missing, so
            // taking the position from there would silently undo that -- and it would look like the canvas
            // losing an edit rather than like recovery doing it.
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);

            var moved = new Rect(new Vector2(750.0f, 640.0f), placeholder.Position.size);
            placeholder.Position = moved;

            var replacement = MissingTypeRecovery.Retarget(
                broken.graph, placeholder, typeof(RetargetTargetNode), out var failure);

            Assert.IsNotNull(replacement, "retarget failed: " + failure);
            Assert.AreEqual(moved.position, replacement.Position.position,
                "the recovered node belongs where the author last put it, not where it was when its type "
                + "disappeared.");
        }

        [Test]
        public void UndoingARetargetBringsBackThePlaceholderAndWhatItHeld()
        {
            // This is the case the whole undo approach was written for. The retarget runs from dropdown
            // callbacks, context menus and a headless command -- none of them inside a canvas draw frame,
            // which is exactly where the mechanism it deliberately avoids records nothing at all and the
            // edit is lost on the next domain reload without ever looking wrong.
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            source.Label = "must come back";
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            string preserved = SinglePlaceholder(broken).formerValue;

            EditorUtility.ClearDirty(broken);
            Assert.IsFalse(EditorUtility.IsDirty(broken), "the fixture must start clean for 'dirty' to mean anything.");

            Assert.IsTrue(
                MissingTypeRetarget.Apply(broken, SinglePlaceholder(broken), typeof(RetargetTargetNode), out var failure),
                "retarget failed: " + failure);

            Assert.IsTrue(EditorUtility.IsDirty(broken),
                "an edit that never marks the asset dirty is an edit that is never written.");
            Assert.AreEqual(1, broken.graph.Nodes.OfType<RetargetTargetNode>().Count());

            Undo.PerformUndo();

            var placeholder = SinglePlaceholder(broken);

            Assert.AreEqual(GoneTypeName, placeholder.formerType,
                "undo has to restore the placeholder, not merely remove the replacement.");
            Assert.AreEqual(preserved, placeholder.formerValue,
                "and it has to restore what the placeholder was holding, or undo loses the only copy of the "
                + "node's data.");
        }

        // ------------------------------------------------------------------ what the tooling reports

        [Test]
        public void VerifyNamesTheFormerTypeAndHowToFixIt()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            RewriteTypeAndReload(TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var findings = BehaviorTreeVerification.Verify(TreePath);
            string joined = string.Join(" | ", findings);

            Assert.IsTrue(findings.Any(f => f.Contains("no longer exists")), joined);
            Assert.IsTrue(findings.Any(f => f.Contains(GoneTypeName)),
                "naming the type is the difference between a report you can act on and one you cannot: " + joined);
            Assert.IsTrue(findings.Any(f => f.Contains("bt_retarget_missing") || f.Contains("RenamedFrom")),
                "the finding has to point at the fix, not just the fault: " + joined);
        }

        [Test]
        public void APlaceholderReportsItselfAsAProblemNamingItsFormerType()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var problems = new System.Collections.Generic.List<NodeProblem>();
            SinglePlaceholder(broken).CollectProblems(problems);

            Assert.AreEqual(1, problems.Count(p => p.Severity == NodeProblemSeverity.Error));
            Assert.IsTrue(problems.Any(p => p.Summary.Contains(GoneTypeName)),
                "the canvas badge is where most people meet this, so it has to name the type.");
            Assert.IsTrue(problems.Any(p => p.Fix != null && p.Fix.Contains("RenamedFrom")),
                "and it has to name the durable fix, not only the manual one.");
        }

        [Test]
        public void ThePlaceholderIsNamedAfterTheTypeItStandsIn()
        {
            var asset = BehaviorTreeAuthoring.CreateTree(TreePath);
            var source = BehaviorTreeAuthoring.AddNode<RetargetSourceNode>(asset, 0.0f, 200.0f);
            BehaviorTreeAuthoring.Connect(asset, asset.graph.EntryNode, source);
            BehaviorTreeAuthoring.Save(asset);

            var broken = RewriteTypeAndReload(
                TreePath, TypeKey(typeof(RetargetSourceNode).FullName), TypeKey(GoneTypeName));

            var placeholder = SinglePlaceholder(broken);

            StringAssert.Contains("NoSuchNodeType", placeholder.NodeName,
                "a canvas full of nodes all reading MISSING TYPE! cannot be triaged; the name is the triage.");
        }
    }

    // ---------------------------------------------------------------------- doubles

    /// <summary>
    /// The node that goes missing: two value inputs and two serialized members, chosen so a retarget onto
    /// <see cref="RetargetTargetNode"/> has something to carry and something to drop.
    /// </summary>
    [GraphCreateMenu("Test/Retarget Source")]
    internal sealed class RetargetSourceNode : BehaviorTreeNode
    {
        [Serialize] public float Speed { get; set; }
        [Serialize] public string Label { get; set; }

        [DoNotSerialize] public ValueInput Alpha { get; private set; }
        [DoNotSerialize] public ValueInput Beta { get; private set; }

        /// <summary>
        /// Declared <b>with</b> defaults, unlike <see cref="Alpha"/> and <see cref="Beta"/>. Only a port that
        /// declares one can hold an inline value across serialization — on a bare port the value lives until
        /// the next reload and no further, which is what <c>SetValue</c> exists to work around. So these are
        /// the only ports that can exercise inline-value carry-over at all.
        /// </summary>
        [DoNotSerialize] public ValueInput Tuning { get; private set; }
        [DoNotSerialize] public ValueInput Legacy { get; private set; }

        public override string NodeName => "Retarget Source";

        protected override void Definition()
        {
            base.Definition();

            Alpha = ValueInput<float>(nameof(Alpha));
            Beta = ValueInput<float>(nameof(Beta));
            Tuning = ValueInput<float>(nameof(Tuning), 1.0f);
            Legacy = ValueInput<float>(nameof(Legacy), 2.0f);
        }
    }

    /// <summary>
    /// What the missing type is retargeted onto: shares <c>Speed</c> and <c>Alpha</c>, and has no home for
    /// <c>Label</c> or <c>Beta</c>.
    /// </summary>
    [GraphCreateMenu("Test/Retarget Target")]
    internal sealed class RetargetTargetNode : BehaviorTreeNode
    {
        [Serialize] public float Speed { get; set; }

        [DoNotSerialize] public ValueInput Alpha { get; private set; }
        [DoNotSerialize] public ValueInput Gamma { get; private set; }

        /// <summary>Shared with the source. <c>Legacy</c> deliberately is not, so an inline value can be dropped.</summary>
        [DoNotSerialize] public ValueInput Tuning { get; private set; }

        public override string NodeName => "Retarget Target";

        protected override void Definition()
        {
            base.Definition();

            Alpha = ValueInput<float>(nameof(Alpha));
            Gamma = ValueInput<float>(nameof(Gamma));
            Tuning = ValueInput<float>(nameof(Tuning), 1.0f);
        }
    }

    /// <summary>
    /// A node that says what it used to be called, so the attribute BH3 tells authors to reach for is pinned
    /// by a test rather than assumed to work.
    /// </summary>
    [GraphCreateMenu("Test/Renamed")]
    [RenamedFrom(PreviousName)]
    internal sealed class RenamedNode : BehaviorTreeNode
    {
        public const string PreviousName = "ArcaneOnyx.BehaviorTree.Tests.NodeUnderItsOldName";

        public override string NodeName => "Renamed";
    }
}

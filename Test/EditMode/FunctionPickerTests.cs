using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneOnyx.BehaviorTree.Authoring;
using ArcaneOnyx.VisualScriptingExtension;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A Script Graph Variable node offers only the Functions that can legally fill the port it feeds
    /// (spec 10, step 2c).
    ///
    /// <para>
    /// The filter is tested rather than the dropdown. The interesting decision — <em>which</em> Functions may
    /// go here — lives in <see cref="FunctionPortConstraint"/> and <see cref="FunctionPickerCatalog"/>
    /// precisely so it can be exercised without an IMGUI event loop; the inspector on top of it is a
    /// dropdown button and three <c>GUI.Button</c> calls, and a test that drove those would be testing
    /// Unity.
    /// </para>
    /// </summary>
    public class FunctionPickerTests
    {
        private const string Folder = "Assets/__FunctionPickerTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "__FunctionPickerTests");
            FunctionEvaluator.InvalidateAll();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            FunctionEvaluator.InvalidateAll();
        }

        // ------------------------------------------------------------------ fixtures

        /// <summary>
        /// A Function declaring a <c>Result</c> of the given type, or none at all when
        /// <paramref name="resultType"/> is null — which is the case that has to stay offerable-to-nobody.
        /// </summary>
        private static FunctionGraphAsset Function(
            string assetName, Type resultType, string subFolder = null, params string[] requiredInputs)
        {
            var function = ScriptableObject.CreateInstance<FunctionGraphAsset>();
            var graph = function.graph;

            graph.units.Add(new ScriptGraphInput { position = new Vector2(-400.0f, 0.0f) });
            graph.units.Add(new ScriptGraphOutput { position = new Vector2(400.0f, 0.0f) });

            graph.controlInputDefinitions.Add(new ControlInputDefinition
            {
                key = FunctionGraphAsset.EnterKey, label = FunctionGraphAsset.EnterKey
            });
            graph.controlOutputDefinitions.Add(new ControlOutputDefinition
            {
                key = FunctionGraphAsset.ExitKey, label = FunctionGraphAsset.ExitKey
            });

            foreach (var key in requiredInputs)
            {
                graph.valueInputDefinitions.Add(new Unity.VisualScripting.ValueInputDefinition
                {
                    key = key, label = key, type = typeof(float)
                });
            }

            if (resultType != null)
            {
                graph.valueOutputDefinitions.Add(new Unity.VisualScripting.ValueOutputDefinition
                {
                    key = FunctionGraphAsset.ResultKey,
                    label = FunctionGraphAsset.ResultKey,
                    type = resultType
                });
            }

            graph.PortDefinitionsChanged();

            var folder = Folder;

            if (subFolder != null)
            {
                folder = Folder;

                // Created a segment at a time: CreateFolder takes one leaf, not a path, and the collision
                // cases need nested folders like Soldier/Functions.
                foreach (var segment in subFolder.Split('/'))
                {
                    var next = $"{folder}/{segment}";
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(folder, segment);
                    folder = next;
                }
            }

            AssetDatabase.CreateAsset(function, $"{folder}/{assetName}.asset");
            return function;
        }

        /// <summary>A node whose Output feeds the <c>Value</c> port of a boolean guard.</summary>
        private static VisualScriptGraphVariable NodeFeedingABooleanGuard()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Caller.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            var guard = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(tree, 200.0f, 0.0f);

            node.Output.ValidlyConnectTo(guard.Value);

            return node;
        }

        private static VisualScriptGraphVariable NodeFeedingNothing()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Lonely.asset");
            return BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
        }

        private static List<string> NamesOf(IEnumerable<FunctionPickerEntry> entries) =>
            entries.Select(entry => entry.Name).ToList();

        // ------------------------------------------------------------------ the constraint

        [Test]
        public void ANodeFeedingABooleanGuard_IsConstrainedToBoolean()
        {
            var constraint = FunctionPortConstraint.For(NodeFeedingABooleanGuard());

            Assert.IsFalse(constraint.IsUnconstrained);
            CollectionAssert.AreEqual(new[] { typeof(bool) }, constraint.RequiredTypes.ToList());
        }

        [Test]
        public void ANodeFeedingNothing_IsUnconstrained()
        {
            var constraint = FunctionPortConstraint.For(NodeFeedingNothing());

            Assert.IsTrue(constraint.IsUnconstrained,
                "authors wire up in whatever order they like, so an unconnected node constrains nothing");
        }

        [Test]
        public void ANodeFeedingTwoPortsOfTheSameType_CarriesThatTypeOnce()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/TwoGuards.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            var first = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(tree, 200.0f, 0.0f);
            var second = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(tree, 200.0f, 200.0f);

            node.Output.ValidlyConnectTo(first.Value);
            node.Output.ValidlyConnectTo(second.Value);

            var constraint = FunctionPortConstraint.For(node);

            CollectionAssert.AreEqual(new[] { typeof(bool) }, constraint.RequiredTypes.ToList(),
                "the same requirement twice is one requirement");
            Assert.IsFalse(constraint.IsMultiplyConstrained);
        }

        [Test]
        public void AConstraintSatisfiedBySubclasses_UsesAssignabilityRatherThanEquality()
        {
            var constraint = FunctionPortConstraint.Requiring(typeof(Component));

            Assert.IsTrue(constraint.Satisfies(typeof(Transform)),
                "a Transform is a Component, and exact-type matching here is the bug the evaluation seam fixed");
            Assert.IsTrue(constraint.Satisfies(typeof(Component)));
            Assert.IsFalse(constraint.Satisfies(typeof(bool)));
        }

        [Test]
        public void AFunctionWithNoResult_IsRefusedEvenWhenNothingConstrainsTheNode()
        {
            var constraint = FunctionPortConstraint.For(NodeFeedingNothing());

            Assert.IsTrue(constraint.IsUnconstrained);
            Assert.IsFalse(constraint.Satisfies(null),
                "this node exists to read a value, so a Function with no Result can never fill it");
        }

        [Test]
        public void TheSuggestedResultType_IsTheOneThatSatisfiesEveryPort()
        {
            Assert.AreEqual(typeof(object), FunctionPortConstraint.For(NodeFeedingNothing()).SuggestedResultType,
                "an unconstrained node still deserves a Function that returns something");

            Assert.AreEqual(typeof(bool), FunctionPortConstraint.For(NodeFeedingABooleanGuard()).SuggestedResultType);
        }

        // ------------------------------------------------------------------ the offer

        [Test]
        public void ABooleanPort_OffersPredicatesAndRefusesOtherFlavours()
        {
            var predicate = Function("IsHurt", typeof(bool));
            Function("ReadHp", typeof(float));
            Function("NoResult", null);

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.For(NodeFeedingABooleanGuard()), AllFunctions());

            CollectionAssert.AreEqual(new[] { "IsHurt" }, NamesOf(offered));
            Assert.AreSame(predicate, offered[0].Function);
        }

        [Test]
        public void AnUnconstrainedNode_OffersEverythingThatReturnsSomething()
        {
            Function("IsHurt", typeof(bool));
            Function("ReadHp", typeof(float));
            Function("NoResult", null);

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.For(NodeFeedingNothing()), AllFunctions());

            CollectionAssert.AreEquivalent(new[] { "IsHurt", "ReadHp" }, NamesOf(offered));
            CollectionAssert.DoesNotContain(NamesOf(offered), "NoResult");
        }

        [Test]
        public void EntriesAreGroupedByFlavour_PredicatesBeforeValues()
        {
            Function("ReadHp", typeof(float));
            Function("IsHurt", typeof(bool));

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.For(NodeFeedingNothing()), AllFunctions());

            Assert.AreEqual(FunctionPickerCatalog.PredicateGroup, offered[0].Group);
            Assert.AreEqual(FunctionPickerCatalog.ValueGroup, offered[1].Group);
        }

        [Test]
        public void AnEntryNamesTheInputsTheCallerWillOwe()
        {
            Function("IsHurt", typeof(bool), null, "threshold");

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.For(NodeFeedingABooleanGuard()), AllFunctions());

            CollectionAssert.AreEqual(new[] { "threshold" }, offered[0].RequiredInputs.ToList());
            Assert.That(offered[0].Label, Does.Contain("threshold"),
                "what the node is about to owe has to be visible before the choice, not after it");
        }

        [Test]
        public void TwoFunctionsWithTheSameName_AreToldApartByTheirFolder()
        {
            Function("IsLowHealth", typeof(bool), "Here");
            Function("IsLowHealth", typeof(bool), "There");

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.For(NodeFeedingABooleanGuard()), AllFunctions());

            Assert.AreEqual(2, offered.Count);
            CollectionAssert.AreEquivalent(
                new[] { "Here", "There" },
                offered.Select(entry => entry.Qualifier).ToList());

            Assert.AreNotEqual(offered[0].Label, offered[1].Label,
                "a choice between two identical rows is worse than no choice at all");
        }

        [Test]
        public void AUniquelyNamedFunction_IsNotQualified()
        {
            Function("IsHurt", typeof(bool));

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.For(NodeFeedingABooleanGuard()), AllFunctions());

            Assert.IsNull(offered[0].Qualifier);
            Assert.AreEqual("IsHurt", offered[0].Label,
                "qualifying every row would lengthen the common case to solve a problem it does not have");
        }

        [Test]
        public void OrderingIsStable_EvenWhenTwoEntriesShareAName()
        {
            Function("IsLowHealth", typeof(bool), "Here");
            Function("IsLowHealth", typeof(bool), "There");

            var constraint = FunctionPortConstraint.For(NodeFeedingABooleanGuard());

            var first = FunctionPickerCatalog.Offer(constraint, AllFunctions())
                .Select(entry => entry.Label).ToList();
            var second = FunctionPickerCatalog.Offer(constraint, AllFunctions())
                .Select(entry => entry.Label).ToList();

            CollectionAssert.AreEqual(first, second,
                "List.Sort is not stable, so tying on name alone lets rows swap between openings");
        }

        // ------------------------------------------------------------------ several ports at once

        [Test]
        public void APortPairNoValueCanSatisfy_SuggestsNothing()
        {
            var constraint = FunctionPortConstraint.Requiring(typeof(bool), typeof(Transform));

            Assert.IsTrue(constraint.IsMultiplyConstrained);
            Assert.IsNull(constraint.SuggestedResultType,
                "a node feeding a bool port and a Transform port asks for something no value can be, and "
                + "inventing a type that fills neither would hide that");
        }

        [Test]
        public void APortPairOneTypeCanSatisfy_SuggestsThatType()
        {
            var constraint = FunctionPortConstraint.Requiring(typeof(Component), typeof(Transform));

            Assert.AreEqual(typeof(Transform), constraint.SuggestedResultType,
                "a Transform fills both a Transform port and a Component one");
        }

        [Test]
        public void SeveralPorts_OfferTheIntersectionRatherThanTheUnion()
        {
            Function("GetTransform", typeof(Transform));
            Function("IsHurt", typeof(bool));

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.Requiring(typeof(bool), typeof(Transform)), AllFunctions());

            CollectionAssert.IsEmpty(offered,
                "each Function fills one of the two ports, and filling one is not filling both");
        }

        // ------------------------------------------------------------------ the rule, at its edges

        [Test]
        public void AConvertibleButUnassignableResult_IsOffered()
        {
            var constraint = FunctionPortConstraint.Requiring(typeof(int));

            Assert.IsTrue(constraint.Satisfies(typeof(float)),
                "the port converts on read, so a float Function works at an int port -- this is the case "
                + "that makes convertibility rather than assignability the right rule");
        }

        [Test]
        public void AnObjectResult_IsOfferedAtATypedPort()
        {
            Assert.IsTrue(FunctionPortConstraint.Requiring(typeof(bool)).Satisfies(typeof(object)),
                "the node's own Output is object and may feed a bool port; a Function returning object is "
                + "offered by that same rule, and pretending otherwise would make the picker stricter than "
                + "the wire it is filling");
        }

        [Test]
        public void AnInvalidConnection_DoesNotConstrainTheNode()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Invalid.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            var guard = BehaviorTreeAuthoring.AddNode<BooleanReactiveGuard>(tree, 200.0f, 0.0f);

            node.Output.InvalidlyConnectTo(guard.Value);

            Assert.IsTrue(FunctionPortConstraint.For(node).IsUnconstrained,
                "already-broken wiring must not dictate what is offered; fixing the wire is the repair, and "
                + "bt_verify reports it");
        }

        // ------------------------------------------------------------------ telling entries apart

        [Test]
        public void SameNamedFunctionsUnderSameNamedFolders_AreQualifiedByEnoughOfThePath()
        {
            Function("IsLowHealth", typeof(bool), "Soldier/Functions");
            Function("IsLowHealth", typeof(bool), "Archer/Functions");

            var offered = FunctionPickerCatalog.Offer(
                FunctionPortConstraint.For(NodeFeedingABooleanGuard()), AllFunctions());

            Assert.AreEqual(2, offered.Count);
            Assert.AreNotEqual(offered[0].Label, offered[1].Label,
                "the immediate parent folder is 'Functions' for both, so the qualifier has to reach further "
                + "up before it distinguishes anything");

            CollectionAssert.AreEquivalent(
                new[] { "Soldier/Functions", "Archer/Functions" },
                offered.Select(entry => entry.Qualifier).ToList());
        }

        [Test]
        public void DescribeAll_QualifiesWithoutFilteringOrSorting()
        {
            var predicate = Function("IsHurt", typeof(bool));
            var value = Function("ReadHp", typeof(float));

            var described = FunctionPickerCatalog.DescribeAll(new[] { predicate, value });

            CollectionAssert.AreEqual(new[] { "IsHurt", "ReadHp" }, NamesOf(described),
                "DescribeAll keeps the caller's order -- it exists for a list that has already been decided, "
                + "such as what a node refused");
        }

        // ------------------------------------------------------------------ the assignment records an undo

        [Test]
        public void AssigningRecordsAgainstTheOwnerItIsGiven_AndDirtiesAnAssetOwner()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Owned.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            var function = Function("IsHurt", typeof(bool));

            EditorUtility.ClearDirty(tree);

            Assert.IsTrue(FunctionAssignment.Apply(node, function, tree));
            Assert.AreSame(function, node.Function);
            Assert.IsTrue(EditorUtility.IsDirty(tree),
                "an assignment that does not dirty its owner is lost on the next domain reload, with nothing "
                + "shown as unsaved");
        }

        /// <summary>
        /// The machine-embedded case, which is the one that regressed: resolving the tree <em>asset</em>
        /// yields null for an embedded nest, so the owner has to be passed in.
        /// </summary>
        [Test]
        public void AssigningRecordsAgainstASceneOwner_ForAMachineEmbeddedTree()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Embedded.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            var function = Function("IsHurt", typeof(bool));

            var host = new GameObject("Agent");

            try
            {
                var machine = host.AddComponent<BehaviorTreeMachine>();

                Assert.IsNull(machine.GraphAsset,
                    "an embedded nest has no macro, which is exactly why resolving the tree asset was the "
                    + "wrong way to find the undo target");

                Assert.IsTrue(FunctionAssignment.Apply(node, function, machine),
                    "the component is a legitimate owner even though it is not an asset");

                Assert.AreSame(function, node.Function);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AssigningWithNoOwner_RefusesRatherThanMutatingUnrecorded()
        {
            var tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Ownerless.asset");
            var node = BehaviorTreeAuthoring.AddNode<VisualScriptGraphVariable>(tree, 0.0f, 0.0f);
            var function = Function("IsHurt", typeof(bool));

            Assert.IsFalse(FunctionAssignment.Apply(node, function, null));
            Assert.IsNull(node.Function,
                "mutating without recording is the silent-loss failure: it would look assigned and be gone "
                + "on the next reload");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Every Function this test created. Deliberately scoped to the test folder rather than searching the
        /// whole project, which would make these assertions depend on the demo content.
        /// </summary>
        private static List<FunctionGraphAsset> AllFunctions() => FunctionGraphAuthoring.FindFunctions(Folder);
    }
}

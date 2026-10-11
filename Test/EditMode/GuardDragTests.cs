using ArcaneOnyx.BehaviorTree.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// A guard sits above its owner and moves only when the owner does. It can be selected, so a lasso picks
    /// it up with whatever is nearby, and a drag begun on one of those nodes must leave it where it is.
    ///
    /// <para>
    /// The rule itself is GraphCore's (a drag moves only what can be dragged) and is tested there against
    /// stand-ins. This pins the half BH3 owns: that the guard's widget is one of the things that cannot.
    /// </para>
    /// </summary>
    public class GuardDragTests
    {
        private const string Folder = "Assets/__GuardDragTests";

        private static readonly Vector2 Delta = new Vector2(120.0f, 60.0f);

        private BehaviorTreeGraphAsset tree;
        private BehaviorTreeCanvas canvas;
        private Sequence owner;
        private WaitTime neighbour;
        private ConditionalExecution guard;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "__GuardDragTests");

            tree = BehaviorTreeAuthoring.CreateTree($"{Folder}/Tree.asset");
            owner = BehaviorTreeAuthoring.AddNode<Sequence>(tree, 0.0f, 0.0f);
            neighbour = BehaviorTreeAuthoring.AddNode<WaitTime>(tree, 400.0f, 0.0f);
            guard = BehaviorTreeAuthoring.GuardOnVariable(tree, owner, "hasTarget", true, false, -200.0f, 0.0f);

            canvas = new BehaviorTreeCanvas(tree.graph);
        }

        [TearDown]
        public void TearDown()
        {
            canvas?.Close();
            canvas?.Dispose();

            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void AGuardSelectedWithAnotherNode_IsNotMovedByDraggingThatNode()
        {
            var guardStart = guard.Position;
            var neighbourStart = neighbour.Position;
            canvas.selection.Add(guard);
            canvas.selection.Add(neighbour);

            Assert.That(canvas.selection.Contains(guard), Is.True,
                "the guard has to be in the selection for the drag to have had the chance to move it");

            canvas.BeginDrag();
            canvas.Drag(Delta, false);
            canvas.EndDrag();

            Assert.That(neighbour.Position.position, Is.Not.EqualTo(neighbourStart.position),
                "the drag has to have moved something, or the next assertion proves nothing");
            Assert.That(guard.Position, Is.EqualTo(guardStart),
                "a guard is placed from its owner; a drag that writes a position into it shows as the guard " +
                "sliding with the selection and jumping back on release");
        }

        [Test]
        public void AGuardSelectedWithItsOwner_IsNotWrittenToByTheDrag()
        {
            var guardStart = guard.Position;
            var ownerStart = owner.Position;
            canvas.selection.Add(guard);
            canvas.selection.Add(owner);

            Assert.That(canvas.selection.Contains(guard), Is.True,
                "the guard has to be in the selection for the drag to have had the chance to move it");

            canvas.BeginDrag();
            canvas.Drag(Delta, false);
            canvas.EndDrag();

            Assert.That(owner.Position.position, Is.Not.EqualTo(ownerStart.position));
            Assert.That(guard.Position, Is.EqualTo(guardStart),
                "no layout pass runs here, so the guard is still where it was authored: dragging its owner " +
                "does not write to it either, and following the owner is left to the guard's own layout");
        }
    }
}

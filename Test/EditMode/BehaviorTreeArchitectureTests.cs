using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneOnyx.GraphCore;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// Structural guards for the behavior tree module rather than behaviour tests. They fail when a new
    /// node is added in a way that compiles but breaks at edit/serialization time — the class of bug that
    /// otherwise only shows up as a node the right-click menu refuses to create, or one that silently
    /// disappears from a graph asset on reload.
    ///
    /// Why these rules matter here specifically:
    ///   * The editor's create menu instantiates a node from its [GraphCreateMenu] type via reflection,
    ///     so every menu entry must point at a concrete <see cref="BehaviorTreeNode"/> with a public
    ///     parameterless constructor.
    ///   * A <see cref="Decorator"/> wraps exactly one child; the whole decorator contract assumes that
    ///     single-child shape, which is enforced through MaxChildrenLimit.
    ///   * <see cref="BehaviorTreeMachine"/> reads its Variables component in Awake, so the
    ///     [RequireComponent] guarantee is what keeps that access safe (and matches the README).
    /// </summary>
    [TestFixture]
    public class BehaviorTreeArchitectureTests
    {
        // The shipping runtime assembly — resolved from a production type so the scan never picks up the
        // ScriptedNode / FixedCondition doubles that live in this test assembly.
        private static readonly Assembly RuntimeAssembly = typeof(BehaviorTreeNode).Assembly;

        private static IEnumerable<Type> RuntimeTypes()
        {
            try
            {
                return RuntimeAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        private static Type[] CreateMenuNodes() =>
            RuntimeTypes()
                .Where(t => t.IsClass && t.GetCustomAttribute<GraphCreateMenu>(false) != null)
                .OrderBy(t => t.Name)
                .ToArray();

        private static Type[] ConcreteDecorators() =>
            RuntimeTypes()
                .Where(t => typeof(Decorator).IsAssignableFrom(t)
                            && t.IsClass && !t.IsAbstract
                            && t != typeof(Decorator))
                .OrderBy(t => t.Name)
                .ToArray();

        [Test]
        public void CreateMenuNodes_Exist()
        {
            // Sanity check so the rules below can't silently pass on an empty set (e.g. an assembly
            // resolution slip that returns no types).
            Assert.IsNotEmpty(CreateMenuNodes(),
                "Expected at least one [GraphCreateMenu] node in the runtime assembly.");
        }

        [Test]
        public void CreateMenuNodes_AreConcreteBehaviorTreeNodes()
        {
            foreach (var type in CreateMenuNodes())
            {
                Assert.IsFalse(type.IsAbstract,
                    $"{type.Name} carries [GraphCreateMenu] but is abstract; the create menu cannot " +
                    $"instantiate it.");
                Assert.IsTrue(typeof(BehaviorTreeNode).IsAssignableFrom(type),
                    $"{type.Name} carries [GraphCreateMenu] but is not a {nameof(BehaviorTreeNode)}.");
            }
        }

        [Test]
        public void CreateMenuNodes_HavePublicParameterlessConstructor()
        {
            foreach (var type in CreateMenuNodes())
            {
                var ctor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

                Assert.IsNotNull(ctor,
                    $"{type.Name} needs a public parameterless constructor so the create menu and Visual " +
                    $"Scripting deserialization can instantiate it.");
            }
        }

        /// <summary>
        /// Two nodes cannot claim the same create-menu path.
        ///
        /// <para>
        /// <c>SetRotation</c> registered on <c>"Unity/Transform/Rotate"</c>, the path <c>Rotate</c> already
        /// owned, and shadowed its <c>NodeName</c> too. One of the two was therefore unreachable from the
        /// menu, and — because both drew as "Rotate" on the canvas — an author looking at an existing tree
        /// could not tell which node it actually contained. Neither symptom looks like a bug from the outside;
        /// it looks like the node behaving strangely.
        /// </para>
        /// </summary>
        [Test]
        public void CreateMenuNodes_DoNotShareAMenuPath()
        {
            var duplicates = CreateMenuNodes()
                .GroupBy(t => t.GetCustomAttribute<GraphCreateMenu>(false).CreateMenuValue)
                .Where(group => group.Count() > 1)
                .Select(group => $"'{group.Key}' is claimed by {string.Join(", ", group.Select(t => t.Name))}")
                .ToArray();

            CollectionAssert.IsEmpty(duplicates,
                "Each create-menu path belongs to exactly one node. A shared path makes one of them "
                + "unreachable from the menu and makes authored assets ambiguous to read:\n"
                + string.Join("\n", duplicates));
        }

        /// <summary>
        /// Every create-menu node defines successfully.
        ///
        /// <para>
        /// <c>Define()</c> catches whatever <c>Definition()</c> throws, logs a warning and then
        /// <c>Undefine()</c>s the node — so a node that fails to define does not announce itself, it simply
        /// arrives with no ports at all. <c>Rotate</c> shipped that way: it declared
        /// <c>ValueInput&lt;float&gt;(nameof(Speed), 0)</c>, and because <c>SetDefaultValue</c> type-checks the
        /// default against the port, a boxed <c>int</c> on a <c>float</c> port threw inside <c>Definition</c>.
        /// One character, and every port on the node was gone.
        /// </para>
        ///
        /// <para>
        /// This is the cheapest possible test of a node — construct it and define it — and the whole
        /// <c>Unity/*</c> leaf family had nothing even this cheap, which is precisely why several of them had
        /// never worked.
        /// </para>
        /// </summary>
        [Test]
        public void CreateMenuNodes_DefineSuccessfully()
        {
            var failed = new List<string>();

            foreach (var type in CreateMenuNodes())
            {
                if (!typeof(BehaviorTreeNode).IsAssignableFrom(type) || type.IsAbstract) continue;

                var node = (BehaviorTreeNode)Activator.CreateInstance(type);
                node.Define();

                if (!node.isDefined) failed.Add(type.Name);
            }

            CollectionAssert.IsEmpty(failed,
                "These nodes threw inside Definition(). Define() swallows that and undefines the node, so "
                + "they reach a graph with no ports rather than reporting anything (check the console for "
                + "'Failed to define'):\n" + string.Join("\n", failed));
        }

        /// <summary>
        /// A node that exposes a port property must actually declare that port in <c>Definition()</c>.
        ///
        /// <para>
        /// <c>Rotate</c> exposed an <c>Axis</c> input and never assigned it, so <c>OnEnter</c>'s
        /// <c>(Vector3)Axis.GetValue()</c> was a guaranteed <see cref="NullReferenceException"/> — the node
        /// broke its branch the first time anything entered it, and could not have worked since the port was
        /// added. Nothing catches this at compile time: the property is simply left null.
        /// </para>
        ///
        /// <para>
        /// Checked structurally rather than by ticking, because it needs no scene, no machine and no
        /// arguments — which is what makes it cheap enough to cover every node in the assembly rather than
        /// the handful someone remembered to write a test for.
        /// </para>
        /// </summary>
        [Test]
        public void CreateMenuNodes_DeclareEveryPortTheyExpose()
        {
            var undeclared = new List<string>();

            foreach (var type in CreateMenuNodes())
            {
                if (!typeof(BehaviorTreeNode).IsAssignableFrom(type) || type.IsAbstract) continue;

                var node = (BehaviorTreeNode)Activator.CreateInstance(type);
                node.Define();

                var portProperties = type
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => typeof(ValueInput).IsAssignableFrom(p.PropertyType)
                                || typeof(ValueOutput).IsAssignableFrom(p.PropertyType));

                foreach (var property in portProperties)
                {
                    if (property.GetValue(node) != null) continue;

                    undeclared.Add($"{type.Name}.{property.Name} ({property.PropertyType.Name})");
                }
            }

            CollectionAssert.IsEmpty(undeclared,
                "These nodes expose a port property that Definition() never assigns, so the first read of it "
                + "throws NullReferenceException and takes the branch down with it:\n"
                + string.Join("\n", undeclared));
        }

        [Test]
        public void Decorators_WrapExactlyOneChild()
        {
            foreach (var type in ConcreteDecorators())
            {
                var decorator = (Decorator)Activator.CreateInstance(type);

                Assert.AreEqual(1, decorator.MaxChildrenLimit,
                    $"{type.Name} is a Decorator and must limit itself to a single child " +
                    $"(MaxChildrenLimit == 1); the decorator contract assumes a one-child shape.");
            }
        }

        [Test]
        public void BehaviorTreeMachine_RequiresVariablesComponent()
        {
            // The machine reads its Variables component in Awake and seeds the "This" key from it;
            // [RequireComponent(typeof(Variables))] is what guarantees the component is present.
            var requireComponents = typeof(BehaviorTreeMachine)
                .GetCustomAttributes(typeof(RequireComponent), true)
                .Cast<RequireComponent>();

            Assert.IsTrue(
                requireComponents.Any(rc =>
                    rc.m_Type0 == typeof(Variables) ||
                    rc.m_Type1 == typeof(Variables) ||
                    rc.m_Type2 == typeof(Variables)),
                $"{nameof(BehaviorTreeMachine)} must [RequireComponent(typeof(Variables))] — it depends on " +
                $"a Variables component being present on the same GameObject.");
        }
    }
}

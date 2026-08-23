using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneOnyx.BehaviorTree.Tests
{
    /// <summary>
    /// The progress bar for the context migration (design spec 07, step 2).
    ///
    /// <para>
    /// BH3 clones the whole tree per agent because per-agent runtime state — timers, current-child indices,
    /// resolved components — lives in fields on node instances. A node written against
    /// <see cref="BTContext"/> keeps that state somewhere the shared-tree refactor can move; a node written
    /// against instance fields and the parameterless hooks does not, and has to be rewritten by hand.
    /// </para>
    ///
    /// <para>
    /// Instance fields are the natural C# thing to write, so without a test the codebase regresses the day
    /// after the seam lands. That is why these rules exist now, well before the refactor they serve: the
    /// expensive thing is not migrating today's nodes, it is the ones written between today and the flip —
    /// including nodes written by users of BH3, whose code is not ours to rewrite.
    /// </para>
    ///
    /// <para>
    /// <b>The allowlists below are a debt register, not a configuration.</b> Every name is a node that still
    /// has to be migrated. Nothing may be added to them — a new offender is a failing build — and
    /// <see cref="LegacyLifecycleAllowlist_HasNoStaleEntries"/> and
    /// <see cref="InstanceStateAllowlist_HasNoStaleEntries"/> make them shrink honestly by failing when a
    /// name is listed that no longer offends. When both lists are empty, spec 07 step 4 can flip the
    /// instancing; until then their length is the remaining work.
    /// </para>
    /// </summary>
    [TestFixture]
    public class NodeContextConventionTests
    {
        // Resolved from a production type, matching BehaviorTreeArchitectureTests — so the scan never picks
        // up the ScriptedNode / FixedCondition doubles that live in this test assembly.
        private static readonly Assembly RuntimeAssembly = typeof(BehaviorTreeNode).Assembly;

        private static readonly string[] LifecycleHooks = { "OnAwake", "OnEnter", "OnUpdate", "OnExit" };

        /// <summary>
        /// Nodes that still override a parameterless lifecycle hook. Shrinks to zero; never grows.
        /// </summary>
        private static readonly HashSet<string> LegacyLifecycleAllowlist = new()
        {
            // Composites and containers — they hold the child-index state that forces cloning, so spec 07
            // migrates this group first.
            "Composite", "ContainerNode", "Entry", "Parallel", "ParallelSelector", "ParallelSequence",
            "RandomSelector", "RandomSequence", "Selector", "Sequence",

            // Conditions and decorators.
            "Condition", "Cooldown", "RandomChance", "Repeater", "ReturnFailure", "ReturnSuccess",
            "UntilFailure", "UntilSuccess",

            // Sub-trees and visual scripting.
            "RunBehaviorTreeGraphNode", "VisualScriptGraphVariable", "VisualScriptingNode",

            // Leaves.
            "AddExplosiveForce", "AddForce", "AddTorque", "CrossFadeAnimation", "DebugLog", "DebugLogError",
            "DebugLogWarning", "DestroyObject", "DontDestroyOnLoad", "FaceTarget",
            "GenerateRandomNavMeshPosition", "InstanteObject", "Literal", "LookAt", "MissingType",
            "PlayAudio", "RemoveVariable", "Rotate", "SetAnimatorTrigger", "SetAnimatorValue",
            "SetNavAgentPosition", "SetPosition", "SetRotation", "SetVariable", "StopNavAgent", "WaitTime",
            "WaitTimeRandomRange", "WaitUntilReachNavTargetPosition"
        };

        /// <summary>
        /// Nodes that still keep per-agent runtime state in instance fields. Shrinks to zero; never grows.
        /// </summary>
        private static readonly HashSet<string> InstanceStateAllowlist = new()
        {
            // The base class itself: guards, scope, recorder and the entry-refusal stamp are all per-agent
            // and all move into instance memory with everything else.
            "BehaviorTreeNode",

            "Composite", "ContainerNode", "Parallel", "Selector", "Sequence",
            "Cooldown", "RandomChance", "ReactiveGuard",
            "RunBehaviorTreeGraphNode", "VisualScriptGraphVariable",
            "PlaceHolderNode", "MissingType", "RemoveVariable",

            "AddExplosiveForce", "AddForce", "AddTorque", "CrossFadeAnimation", "LookAt", "PlayAudio",
            "Rotate", "SetAnimatorTrigger", "SetAnimatorValue", "SetNavAgentPosition", "SetPosition",
            "SetRotation", "StopNavAgent", "WaitTime", "WaitTimeRandomRange",
            "WaitUntilReachNavTargetPosition"
        };

        private static IEnumerable<Type> NodeTypes()
        {
            IEnumerable<Type> types;

            try
            {
                types = RuntimeAssembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null);
            }

            return types.Where(t => t.IsClass && typeof(BehaviorTreeNode).IsAssignableFrom(t))
                        .OrderBy(t => t.Name);
        }

        private const BindingFlags Declared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>The parameterless lifecycle hooks this type overrides, if any.</summary>
        private static string[] LegacyHooksOverriddenBy(Type type) =>
            LifecycleHooks
                .Where(hook => type.GetMethod(hook, Declared, null, Type.EmptyTypes, null) != null)
                .ToArray();

        /// <summary>
        /// The per-agent state fields this type declares, if any.
        ///
        /// <para>
        /// Ports are excluded: a <c>ValueInput</c> is the node's shape, not its state, and it stays on the
        /// shared structure. Serialized fields are excluded for the same reason — an authored setting is one
        /// value for every agent running the tree. What is left is what differs per agent and therefore has
        /// to move.
        /// </para>
        ///
        /// <para>
        /// An auto-property's backing field carries none of the property's attributes, so the property is
        /// what gets asked. Without that, every <c>[Serialize] public string X { get; private set; }</c>
        /// would read as unserialized state and the list would be mostly noise.
        /// </para>
        ///
        /// <para>
        /// <b>Known limitation: <c>[Serialize]</c> is a trust boundary, not a check.</b> This asks whether a
        /// field is serialized, not whether the node writes to it while it runs, so
        /// <c>[Serialize] private float timer;</c> passes. That is worse than the failure it silences — a
        /// serialized field belongs to the shared tree, so after the instancing flip it is one value every
        /// agent writes in turn, with nothing left to report it. Catching it needs the assignment sites
        /// rather than the declaration: an IL scan of the four lifecycle bodies for <c>stfld</c> against a
        /// serialized field, or a source scan in the style of <c>PortReadConventionTests</c>. Neither is
        /// here yet; <c>custom-nodes.md</c> tells authors not to do it.
        /// </para>
        /// </summary>
        private static string[] PerAgentStateFieldsOn(Type type)
        {
            var offenders = new List<string>();

            foreach (var field in type.GetFields(Declared))
            {
                if (field.IsStatic || field.IsLiteral) continue;

                var declaredType = field.FieldType;
                var attributes = field.GetCustomAttributes(true);
                var name = field.Name;

                if (TryGetBackingPropertyName(field.Name, out var propertyName))
                {
                    var property = type.GetProperty(propertyName, Declared);
                    if (property != null)
                    {
                        declaredType = property.PropertyType;
                        attributes = property.GetCustomAttributes(true);
                    }

                    name = propertyName;
                }

                if (IsPort(declaredType)) continue;
                if (IsSerialized(attributes)) continue;

                offenders.Add(name);
            }

            return offenders.ToArray();
        }

        private static bool TryGetBackingPropertyName(string fieldName, out string propertyName)
        {
            propertyName = null;

            if (!fieldName.StartsWith("<") || !fieldName.EndsWith(">k__BackingField")) return false;

            propertyName = fieldName.Substring(1, fieldName.IndexOf('>') - 1);
            return true;
        }

        private static bool IsPort(Type type)
        {
            var name = type.Name;

            return name.StartsWith("ValueInput") || name.StartsWith("ValueOutput")
                || name.StartsWith("ControlInput") || name.StartsWith("ControlOutput")
                || name.StartsWith("InvalidInput") || name.StartsWith("InvalidOutput")
                || name.StartsWith("IPortCollection");
        }

        private static bool IsSerialized(IEnumerable<object> attributes) =>
            attributes.Any(a => a.GetType().Name is "SerializeAttribute" or "SerializeFieldAttribute");

        [Test]
        public void Scan_FindsTheNodesItIsSupposedTo()
        {
            // A sanity floor, in the style of BehaviorTreeArchitectureTests: without it every rule below
            // passes vacuously if the assembly ever resolves to nothing.
            Assert.Greater(NodeTypes().Count(), 50,
                "Expected the runtime assembly to contain the full node family; the scan resolved almost "
                + "nothing, so every rule in this fixture would pass without checking anything.");
        }

        [Test]
        public void NoNewNode_OverridesALegacyLifecycleHook()
        {
            var offenders = NodeTypes()
                .Where(type => !LegacyLifecycleAllowlist.Contains(type.Name))
                .Select(type => new { type, hooks = LegacyHooksOverriddenBy(type) })
                .Where(entry => entry.hooks.Length > 0)
                .Select(entry => $"{entry.type.Name} overrides {string.Join(", ", entry.hooks.Select(hook => hook + "()"))}")
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "These nodes override a parameterless lifecycle hook. Write the body against the "
                + "context-taking overload instead -- OnEnter(BTContext ctx) -- and read per-agent state "
                + "from ctx rather than from this. The parameterless hooks exist only to keep nodes written "
                + "before the seam working, and every new use of them is work the shared-tree refactor will "
                + "have to undo by hand:\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void NoNewNode_KeepsPerAgentStateInInstanceFields()
        {
            var offenders = NodeTypes()
                .Where(type => !InstanceStateAllowlist.Contains(type.Name))
                .Select(type => new { type, fields = PerAgentStateFieldsOn(type) })
                .Where(entry => entry.fields.Length > 0)
                .Select(entry => $"{entry.type.Name} declares {string.Join(", ", entry.fields)}")
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "These nodes keep per-agent runtime state in instance fields. That is what forces BH3 to "
                + "deep-clone the whole tree for every agent, and it is the one thing the shared-tree "
                + "refactor cannot do for you. A timer, a current-child index or a component resolved in "
                + "OnEnter belongs in a per-node memory type reached through BTContext; an authored setting "
                + "belongs on a [Serialize] field, which is not flagged:\n"
                + string.Join("\n", offenders));
        }

        [Test]
        public void LegacyLifecycleAllowlist_HasNoStaleEntries()
        {
            var byName = NodeTypes().ToDictionary(type => type.Name, type => type);

            var stale = LegacyLifecycleAllowlist
                .OrderBy(name => name)
                .Select(name => !byName.TryGetValue(name, out var type)
                    ? $"{name} is listed but no longer exists in the runtime assembly"
                    : LegacyHooksOverriddenBy(type).Length == 0
                        ? $"{name} is listed but no longer overrides any parameterless hook"
                        : null)
                .Where(problem => problem != null)
                .ToArray();

            CollectionAssert.IsEmpty(stale,
                "This list is a debt register and it is only honest while every name on it still owes "
                + "something. Remove these -- migrating a node and leaving it listed hides the next node "
                + "that regresses into the same mistake:\n"
                + string.Join("\n", stale));
        }

        [Test]
        public void InstanceStateAllowlist_HasNoStaleEntries()
        {
            var byName = NodeTypes().ToDictionary(type => type.Name, type => type);

            var stale = InstanceStateAllowlist
                .OrderBy(name => name)
                .Select(name => !byName.TryGetValue(name, out var type)
                    ? $"{name} is listed but no longer exists in the runtime assembly"
                    : PerAgentStateFieldsOn(type).Length == 0
                        ? $"{name} is listed but no longer declares per-agent instance state"
                        : null)
                .Where(problem => problem != null)
                .ToArray();

            CollectionAssert.IsEmpty(stale,
                "This list is a debt register and it is only honest while every name on it still owes "
                + "something. Remove these:\n"
                + string.Join("\n", stale));
        }
    }
}

using System;
using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// Stands in for a node whose C# type no longer exists, and keeps everything that node held so it can be
    /// put back — by <see cref="MissingTypeRecovery"/> when the type resolves again, or by retargeting it
    /// onto a replacement type from the inspector.
    ///
    /// <para>
    /// It is a container, not a tombstone. Deleting a node type is the accepted way to retire one here, and
    /// renaming or moving one is routine, so the placeholder's job is to make that reversible.
    /// </para>
    /// </summary>
    public sealed class MissingType : BehaviorTreeNode
    {
        /// <summary>
        /// The type name exactly as the asset named it. Written by
        /// <see cref="MissingTypeSerialization"/> before deserialization, because by the time this object
        /// exists the name is the only trace of what it used to be.
        /// </summary>
        [Serialize]
        public string formerType { get; private set; } // Private set is required by the deserializer.

        /// <summary>
        /// The node's own JSON, verbatim, as it was written by the last version that still had the type.
        ///
        /// <para>
        /// Kept as text rather than re-parsed because nothing here can interpret it: the type is gone, so
        /// there is no shape to validate it against. It becomes a node again by being handed back to the
        /// serializer together with <see cref="formerObjects"/>, which is the whole reason recovery needs no
        /// document surgery of its own.
        /// </para>
        /// </summary>
        [Serialize]
        public string formerValue { get; private set; }

        /// <summary>
        /// The asset's object table as it stood when this placeholder was made, and the reason a recovered
        /// node still points at the right assets.
        ///
        /// <para>
        /// A Unity object inside a node serializes as a bare integer index into the asset's object table —
        /// indistinguishable, in the raw document, from any other integer. So <see cref="formerValue"/>
        /// cannot have its references rewritten to anything more durable, and instead the table those
        /// indices refer to is kept whole, here, on the placeholder. Holding the objects is also what stops
        /// them being dropped: the table is rebuilt from whatever the live objects reference on every save,
        /// so a reference nothing holds any more simply disappears.
        /// </para>
        ///
        /// <para>
        /// Assigned once, by <see cref="BehaviorTreeGraphAsset"/>, on the load that creates the placeholder.
        /// A placeholder that survives a save already carries its own copy and must never be given the
        /// current asset's table, which is a different table with different indices.
        /// </para>
        /// </summary>
        [Serialize]
        public List<UnityObject> formerObjects { get; private set; }

        public override string NodeName =>
            string.IsNullOrEmpty(formerType) ? "MISSING TYPE!" : $"MISSING: {ShortFormerTypeName}";

        /// <summary>The former type without its namespace, for surfaces with no room for the full name.</summary>
        public string ShortFormerTypeName
        {
            get
            {
                if (string.IsNullOrEmpty(formerType)) return string.Empty;

                int lastDot = formerType.LastIndexOf('.');
                return lastDot < 0 ? formerType : formerType.Substring(lastDot + 1);
            }
        }

        /// <summary>
        /// Whether this placeholder still holds what the node contained. False for one written before the
        /// placeholder kept anything, where nothing but the position and inline values can be recovered.
        /// </summary>
        public bool HasPreservedState => !string.IsNullOrEmpty(formerValue);

        public override string Description =>
            string.IsNullOrEmpty(formerType)
                ? "This node's type no longer exists. Nothing was preserved, so it can only be replaced."
                : $"This node stands in for '{formerType}', whose script no longer exists. Re-add the script, "
                  + "add [RenamedFrom] to whatever replaced it, or pick a replacement type below.";

        /// <summary>
        /// Resolves <see cref="formerType"/> against the types that exist now, honouring the
        /// <c>[RenamedFrom]</c> / <c>[RenamedNamespace]</c> / <c>[RenamedAssembly]</c> attributes the
        /// serializer honours. Recovery is possible exactly when this succeeds and the result is a node.
        /// </summary>
        public bool TryResolveFormerType(out Type type)
        {
            type = null;

            return !string.IsNullOrEmpty(formerType)
                && RuntimeCodebase.TryDeserializeType(formerType, out type);
        }

        /// <summary>Assigns the object table this placeholder's <see cref="formerValue"/> indexes into.</summary>
        internal void CaptureFormerObjects(IEnumerable<UnityObject> objectReferences)
        {
            formerObjects = objectReferences == null ? new List<UnityObject>() : new List<UnityObject>(objectReferences);
        }

        // Although this unit will have no ports, the already existing graph
        // connections will create invalid ones to connect themselves to.
        protected override void Definition() { }

        [DoNotSerialize]
        private bool hasAnnouncedItself;

        /// <summary>
        /// Says once, out loud, that a node which no longer exists just reported success.
        ///
        /// <para>
        /// The status itself is deliberately unchanged — see the design doc; whether a gap should read as
        /// <c>Success</c> or <c>Failure</c> depends on whether its parent is a Sequence or a Selector, and
        /// that is its own decision. What is not defensible either way is doing it <em>silently</em>: a
        /// placeholder has nothing to run and nothing to fail, so a Sequence walks straight past it and the
        /// branch quietly does less than it used to, with no exception and nothing in the log. This is the
        /// feature's whole premise, and until now it was the one place the premise was not acted on.
        /// </para>
        ///
        /// <para>
        /// Once per node per run, behind a flag, and only on a node that exists solely in a broken tree — so
        /// nothing on the healthy path pays for it, and a tree with a hole in it says so in a build's log
        /// rather than only in the editor.
        /// </para>
        /// </summary>
        public override ExecutionStatus OnUpdate()
        {
            if (!hasAnnouncedItself)
            {
                hasAnnouncedItself = true;

                Debug.LogWarning(
                    $"[BH3] A node whose type no longer exists ('{(string.IsNullOrEmpty(formerType) ? "unknown" : formerType)}') "
                    + "just ran and reported success, because there is nothing left of it to run. Whatever "
                    + "this branch used to do here, it is not doing.");
            }

            return base.OnUpdate();
        }

        /// <summary>
        /// A node standing in for a type that no longer exists is wrong in every configuration, so it says
        /// so on the canvas rather than relying on its name being read. This is how the instances left in
        /// a tree by a deleted node type -- RunScriptGraph was the first -- get found.
        /// </summary>
        public override void CollectProblems(List<NodeProblem> into)
        {
            base.CollectProblems(into);

            if (string.IsNullOrEmpty(formerType))
            {
                into.Add(new NodeProblem(NodeProblemSeverity.Error,
                    "This node's type no longer exists, so it does nothing.",
                    "Pick a replacement type in the inspector, or delete it."));
                return;
            }

            // Resolving here means the type came back but the node did not, which is a different problem
            // with a different fix -- and one the author cannot diagnose from "the type is missing".
            if (TryResolveFormerType(out var resolved))
            {
                into.Add(typeof(BehaviorTreeNode).IsAssignableFrom(resolved)
                    ? new NodeProblem(NodeProblemSeverity.Error,
                        $"'{formerType}' exists again as '{resolved.FullName}', but this node could not be "
                        + "rebuilt from what was preserved.",
                        HasPreservedState
                            ? "See the console for why the rebuild failed; retargeting it onto the same type "
                              + "from the inspector reports the same reason."
                            : "Nothing was preserved for this node, so retarget it onto that type in the "
                              + "inspector and set its values again.")
                    : new NodeProblem(NodeProblemSeverity.Error,
                        $"'{formerType}' exists again as '{resolved.FullName}', but it is not a behavior tree "
                        + "node, so this node could not be restored.",
                        $"Make {resolved.Name} derive from {nameof(BehaviorTreeNode)}, or retarget this node "
                        + "onto a type that does."));
                return;
            }

            into.Add(new NodeProblem(NodeProblemSeverity.Error,
                $"This node's type ('{formerType}') no longer exists, so it does nothing.",
                "Re-add its script, add [RenamedFrom(\"" + formerType + "\")] to whatever replaced it, or "
                + "retarget this node onto a replacement type in the inspector."));
        }
    }
}

using System.Linq;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;
using UnityEngine;

namespace ArcaneOnyx.BehaviorTree
{
    [CreateAssetMenu(menuName = "Visual Scripting/Behavior Tree", fileName = "New Behavior Tree Graph", order = 81)]
    public class BehaviorTreeGraphAsset : BaseGraphAsset<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        [Serialize, Inspectable]
        public VariableDeclarations declarations { get; internal set; } = new() { Kind = VariableKind.Graph };

        /// <summary>
        /// What an agent running this tree must declare on its Variables component. Reading a variable that
        /// nothing supplies throws, so a branch reused on an agent that lacks one fails at the tick rather
        /// than at author time — this is the list that makes that checkable before play.
        /// <para>
        /// Nothing is read from here at runtime. It is a contract, and the values only carry the type so the
        /// Blackboard panel can edit it with the same inspector as any other declaration.
        /// </para>
        /// </summary>
        [Serialize, Inspectable]
        public VariableDeclarations requiredDeclarations { get; internal set; } = new() { Kind = VariableKind.Graph };

        /// <summary>
        /// Values this tree supplies for itself when the agent does not. Applied to the root tree's
        /// declarations by <c>BehaviorTreeMachine.OverrideGraphVariables</c>, and always losing to
        /// the agent, so a branch can ship a sensible default and only genuinely agent-specific values need
        /// to appear in <see cref="requiredDeclarations"/>.
        /// </summary>
        [Serialize, Inspectable]
        public VariableDeclarations optionalDeclarations { get; internal set; } = new() { Kind = VariableKind.Graph };

        //[ContextMenu("Show Data...")]
        protected override void ShowData()
        {
            base.ShowData();
        }

        public override BehaviorTreeGraph DefaultGraph()
        {
            return BehaviorTreeGraph.CreateEmpty();
        }
    
        /// <summary>
        /// The object table the placeholders made by <em>this</em> load index into, held between the two
        /// deserialization callbacks because that is the only window in which both halves exist: the table
        /// lives on <c>_data</c>, which is cleared as soon as deserialization finishes, and the placeholders
        /// that need it are not objects yet.
        /// </summary>
        [DoNotSerialize]
        private UnityEngine.Object[] convertedObjectReferences;

        protected override void OnBeforeDeserialize()
        {
            if (MissingTypeSerialization.Convert(ref _data, out var convertedTypeNames))
            {
                convertedObjectReferences = _data.objectReferences;

                // Passed as the log's context object rather than named in the message: reading `name` here
                // throws "GetName is not allowed to be called during serialization", and because the throw
                // escapes into Unity's deserialization it takes the whole asset down with it -- every node
                // silently absent, which looks nothing like the logging mistake it is. The context object
                // makes the entry click through to the asset anyway.
                Debug.LogWarning(
                    $"[BH3] This tree refers to {convertedTypeNames.Count} node type(s) that no longer exist: "
                    + string.Join(", ", convertedTypeNames.Distinct())
                    + ". Each one is now a placeholder holding what the node contained -- re-add the script, "
                    + "add [RenamedFrom] to whatever replaced it, or retarget the node in the inspector.",
                    this);
            }

            base.OnBeforeDeserialize();
        }

        /// <summary>
        /// Finishes what <see cref="OnBeforeDeserialize"/> started, in the order the two halves demand: the
        /// placeholders are given the object table their preserved state indexes into, and then every
        /// placeholder whose type resolves again — because a script came back, or because someone added
        /// <c>[RenamedFrom]</c> to its replacement — becomes a real node again.
        /// </summary>
        protected override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();

            if (graph == null) return;

            if (convertedObjectReferences != null)
            {
                foreach (var placeholder in graph.Nodes.OfType<MissingType>())
                {
                    // Only the ones this load created. A placeholder that survived a save carries its own
                    // table already, and the current asset's table is a different one with different indices.
                    if (!placeholder.HasPreservedState || placeholder.formerObjects != null) continue;

                    placeholder.CaptureFormerObjects(convertedObjectReferences);
                }

                convertedObjectReferences = null;
            }

            int restored = MissingTypeRecovery.RestoreResolvableNodes(graph);

            if (restored > 0)
            {
                Debug.Log($"[BH3] Restored {restored} node(s) whose type exists again.", this);
            }
        }
    }
}
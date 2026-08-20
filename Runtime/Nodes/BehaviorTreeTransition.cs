using System.Collections.Generic;
using ArcaneOnyx.GraphCore;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    public class BehaviorTreeTransition : BaseGraphTransition<BehaviorTreeGraph, BehaviorTreeNode, BehaviorTreeTransition>
    {
        /// <summary>
        /// Where a tree written before placeholders became editor-only stored them (serialized key
        /// <c>PlaceHolderNodes</c>), kept for reading and nothing else.
        ///
        /// <para>
        /// <b>It cannot simply be deleted.</b> In those files the placeholder objects are <em>defined</em>
        /// inside this field and the graph's element list refers to them by id, so a version that does not
        /// deserialize it leaves those references pointing at nothing and the whole asset fails to load —
        /// not the transition, the entire tree. Every tree in the project is in that state today.
        /// </para>
        ///
        /// <para>
        /// Nothing writes it any more. <see cref="BehaviorTreeGraph.OnAfterDependenciesDeserialized"/> clears
        /// it once the ids have served their purpose, so the field serializes as null and the stored
        /// placeholders leave the asset the first time it is saved.
        /// </para>
        /// </summary>
        // The literal is the key already sitting in every tree asset on disk, so it is not free to change.
        [SerializeAs("PlaceHolderNodes")]
        private List<PlaceHolderNode> legacyPlaceHolderNodes;

        /// <summary>
        /// Drops the legacy list once deserialization has finished with it. See
        /// <see cref="legacyPlaceHolderNodes"/> for why the field is read at all.
        /// </summary>
        public void DiscardLegacyPlaceHolderNodes() => legacyPlaceHolderNodes = null;
    }
}

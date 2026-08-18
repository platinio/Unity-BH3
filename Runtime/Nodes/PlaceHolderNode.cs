using System;
using Unity.VisualScripting;

namespace ArcaneOnyx.BehaviorTree
{
    /// <summary>
    /// placeholder node use to create invisible nodes in the canvas, currently used to fake the selection of the node transition
    /// </summary>
    public class PlaceHolderNode : BehaviorTreeNode
    {
        [Serialize] private BehaviorTreeTransition owner;
        public BehaviorTreeTransition Owner => owner;
        
        public override bool CanSelect => true;
        public override bool CanDrag => false;
        public override bool CanDelete => true;
        public override bool IsVisible => false;

        public bool IsSelected = false;
        
        /// <summary>
        /// For the serializer only. Without it this type has no parameterless constructor, so
        /// deserialization cannot call a constructor at all and materialises the object directly — which
        /// skips every field initialiser on the class, including the ones inherited from
        /// <see cref="BehaviorTreeNode"/>. Every other node type already has one, so this was the only node
        /// arriving half-built.
        /// </summary>
        [Obsolete(Serialization.ConstructorWarning)]
        public PlaceHolderNode() { }

        public PlaceHolderNode(BehaviorTreeTransition owner)
        {
            this.owner = owner;
        }
    }
}
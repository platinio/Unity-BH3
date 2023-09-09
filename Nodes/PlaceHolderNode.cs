using Unity.VisualScripting;

namespace Platinio.BehaviorTree
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
        
        public PlaceHolderNode(BehaviorTreeTransition owner)
        {
            this.owner = owner;
        }
    }
}
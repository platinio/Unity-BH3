using Unity.VisualScripting;

namespace Platinio.BehaviourTree
{
    /// <summary>
    /// placeholder node use to create invisible nodes in the canvas, currently used to fake the selection of the node transition
    /// </summary>
    public class PlaceHolderNode : BehaviourTreeNode
    {
        [Serialize] private BehaviourTreeTransition m_owner;
        public BehaviourTreeTransition Owner => m_owner;
        
        public override bool CanSelect => true;
        public override bool CanDrag => false;
        public override bool CanDelete => true;
        public override bool IsVisible => false;

        public bool IsSelected = false;
        
        public PlaceHolderNode(BehaviourTreeTransition owner)
        {
            m_owner = owner;
        }
    }
}